using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Connections;

namespace DotNext.Net.Multiplexing;

using Buffers;
using Threading;

partial class MultiplexedListener
{
    private static void AddRemoteAddress(ref TagList measurementTags, EndPoint? remoteEndPoint)
    {
        if (remoteEndPoint is not null)
        {
            measurementTags.Add(ClientAddressMeterAttribute, remoteEndPoint.ToString());
        }
    }

    private TagList CreateMeasurementTags(Socket socket)
    {
        var tags = measurementTags;
        AddRemoteAddress(ref tags, socket.RemoteEndPoint);
        return tags;
    }
    
    private async Task DispatchAsync(Socket socket)
    {
        var writeSignal = new AsyncAutoResetEvent(initialState: false);
        var receiveBuffer = allocator(flushThreshold);
        var framingBuffer = new PoolingBufferWriter<byte>(allocator) { Capacity = flushThreshold };
        var input = new InputMultiplexer<MultiplexedListener>(
            new(),
            writeSignal,
            framingBuffer,
            flushThreshold,
            CreateMeasurementTags(socket),
            timeout,
            heartbeatTimeout,
            lifetimeToken);
        var receiveTokenSource = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
        var output = input.CreateOutput(
            receiveBuffer.Memory,
            timeout,
            streamFactory,
            receiveTokenSource.Token);
        
        // send loop
        try
        {
            var receiveLoop = output.ProcessAsync(socket);
            while (true)
            {
                try
                {
                    // rethrow exception from the receiving loop
                    await (receiveLoop.IsCompleted
                            ? receiveLoop
                            : input.ProcessAsync(receiveLoop.IsNotCompleted, socket))
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException e) when (e.CancellationToken == lifetimeToken)
                {
                    await receiveLoop.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                    await input
                        .CompleteAllAsync(new ConnectionResetException(ExceptionMessages.ConnectionClosed, e))
                        .ConfigureAwait(false);
                    break;
                }
                catch (Exception e)
                {
                    await receiveTokenSource.CancelAsync().ConfigureAwait(false);
                    await receiveLoop.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                    await input.CompleteAllAsync(e).ConfigureAwait(false);
                    break;
                }
            }
        }
        finally
        {
            receiveTokenSource.Dispose();
            await input.DisposeAsync().ConfigureAwait(false);
            await output.DisposeAsync().ConfigureAwait(false);
            writeSignal.Dispose();
            receiveBuffer.Dispose();
            framingBuffer.Dispose();
            await socket.DisconnectAsync(timeout).ConfigureAwait(false);
        }
    }
}
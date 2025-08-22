using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Connections;

namespace DotNext.Net.Multiplexing;

using Buffers;
using Threading;

partial class MultiplexedClient
{
    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private readonly AsyncAutoResetEvent writeSignal;

    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private readonly BufferWriter<byte> framingBuffer;
    private readonly PipeOptions options;
    
    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private readonly InputMultiplexer<MultiplexedClient> input;
    
    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private readonly OutputMultiplexer<MultiplexedClient> output;
    private uint streamId;

    private void ReportConnected()
        => Interlocked.Exchange(ref readiness, null)?.TrySetResult();

    private void ReportDisconnected()
    {
        if (readiness is null)
        {
            var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Interlocked.CompareExchange(ref readiness, source, null);
        }
    }

    private void ReportDisposed()
    {
        var e = new ObjectDisposedException(GetType().Name);
        ExceptionDispatchInfo.SetCurrentStackTrace(e);
        if (readiness?.TrySetException(e) is null)
        {
            var source = new TaskCompletionSource();
            source.SetException(e);
            Interlocked.CompareExchange(ref readiness, source, null);
        }
    }

    private async Task DispatchAsync()
    {
        var socket = default(Socket);
        var receiveLoop = Task.CompletedTask;

        // send loop
        while (true)
        {
            try
            {
                // rethrow exception from the receiving loop
                if (receiveLoop.IsCompleted)
                    await receiveLoop.ConfigureAwait(false);

                // connect if needed
                if (socket is null)
                {
                    socket = await ConnectAsync(input.RootToken).ConfigureAwait(false);
                    receiveLoop = output.ProcessAsync(socket);
                    ReportConnected();
                }

                // send data
                await input.ProcessAsync(receiveLoop.IsNotCompleted, socket).ConfigureAwait(false);
            }
            catch (OperationCanceledException e) when (e.CancellationToken == input.RootToken)
            {
                await receiveLoop.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                await input.CompleteAllAsync(new ConnectionResetException(ExceptionMessages.ConnectionClosed, e))
                    .ConfigureAwait(false);

                break;
            }
            catch (Exception e)
            {
                socket?.Dispose();
                await receiveLoop.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                
                ReportDisconnected();
                await input.CompleteAllAsync(e).ConfigureAwait(false);
            }
        }

        if (socket is not null)
            await socket.DisconnectAsync(input.Timeout).ConfigureAwait(false);
    }
}
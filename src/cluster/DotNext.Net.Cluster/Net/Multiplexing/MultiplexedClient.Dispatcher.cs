using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net.Sockets;
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
    private readonly InputMultiplexer input;
    
    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private readonly OutputMultiplexer output;
    private ulong streamId;

    private async Task DispatchAsync()
    {
        var socket = default(Socket);
        var receiveLoop = Task.CompletedTask;

        // send loop
        while (!input.Token.IsCancellationRequested)
        {
            try
            {
                // rethrow exception from the receiving loop
                if (receiveLoop.IsCompleted)
                    await receiveLoop.ConfigureAwait(false);

                // connect if needed
                if (socket is null)
                {
                    socket = await ConnectAsync(input.Token).ConfigureAwait(false);
                    receiveLoop = output.ProcessAsync(socket);
                    readiness.TrySetResult();
                }

                // send data
                await input.ProcessAsync(receiveLoop.IsNotCompleted, socket).ConfigureAwait(false);
            }
            catch (OperationCanceledException e) when (e.CancellationToken == input.Token)
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
                await input.CompleteAllAsync(e).ConfigureAwait(false);
            }
        }

        if (socket is not null)
            await socket.DisconnectAsync(input.Timeout).ConfigureAwait(false);
    }
}
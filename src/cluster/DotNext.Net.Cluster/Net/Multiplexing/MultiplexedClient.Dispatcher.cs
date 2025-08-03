using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Connections;

namespace DotNext.Net.Multiplexing;

using Threading;

partial class MultiplexedClient
{
    private readonly ConcurrentDictionary<ulong, StreamHandler> streams;
    private readonly byte[] sendBuffer, receiveBuffer;
    
    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private readonly AsyncAutoResetEvent writeSignal;
    private readonly PipeOptions options;
    private ulong streamId;

    private async Task DispatchAsync()
    {
        Debugger.NotifyOfCrossThreadDependency();
        var input = new InputMultiplexer(streams, writeSignal, sendBuffer, timeout, heartbeatTimeout, lifetimeToken);
        var output = input.CreateOutput(receiveBuffer, timeout);
        
        var socket = default(Socket);
        var receiveLoop = Task.CompletedTask;

        // send loop
        while (!lifetimeToken.IsCancellationRequested)
        {
            try
            {
                // rethrow exception from the receiving loop
                if (receiveLoop.IsCompleted)
                    await receiveLoop.ConfigureAwait(false);

                // connect if needed
                if (socket is null)
                {
                    socket = await ConnectAsync(lifetimeToken).ConfigureAwait(false);
                    receiveLoop = output.ProcessAsync(socket);
                    readiness.TrySetResult();
                }

                // send data
                await input.ProcessAsync(receiveLoop.IsNotCompleted, socket).ConfigureAwait(false);
            }
            catch (OperationCanceledException e) when (e.CancellationToken == lifetimeToken)
            {
                await receiveLoop.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                await input.CompleteAllAsync(new ConnectionResetException(ExceptionMessages.ConnectionClosed, e))
                    .ConfigureAwait(false);

                break;
            }
            catch (Exception e)
            {
                Debug.Assert(socket is not null);

                socket.Dispose();
                socket = null;
                await receiveLoop.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                await input.CompleteAllAsync(e).ConfigureAwait(false);
            }
        }

        await input.DisposeAsync().ConfigureAwait(false);
        await output.DisposeAsync().ConfigureAwait(false);

        if (socket is not null)
            await socket.DisconnectAsync(timeout).ConfigureAwait(false);
    }
}
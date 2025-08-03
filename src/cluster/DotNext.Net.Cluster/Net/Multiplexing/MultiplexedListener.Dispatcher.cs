using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Connections;

namespace DotNext.Net.Multiplexing;

using Threading;

partial class MultiplexedListener
{
    private async Task DispatchAsync(Socket socket)
    {
        Debugger.NotifyOfCrossThreadDependency();
        var writeSignal = new AsyncAutoResetEvent(initialState: false);
        var sendBuffer = options.Pool.Rent(fragmentSize);
        var receiveBuffer = options.Pool.Rent(fragmentSize);
        var input = new InputMultiplexer(new(), writeSignal, sendBuffer.Memory, timeout, heartbeatTimeout, lifetimeToken);
        var receiveTokenSource = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
        var output = input.CreateOutput(
            receiveBuffer.Memory,
            timeout,
            () => CreateHandler(writeSignal),
            receiveTokenSource.Token);
        
        // send loop
        try
        {
            var receiveLoop = output.ProcessAsync(socket);
            
            while (!lifetimeToken.IsCancellationRequested)
            {
                // rethrow exception from the receiving loop
                if (receiveLoop.IsCompleted)
                    await receiveLoop.ConfigureAwait(false);

                try
                {
                    await input.ProcessAsync(receiveLoop.IsNotCompleted, socket).ConfigureAwait(false);
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
            sendBuffer.Dispose();
            receiveBuffer.Dispose();
            await socket.DisconnectAsync(timeout).ConfigureAwait(false);
        }
    }
    
    
}
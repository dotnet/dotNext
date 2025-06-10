using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace DotNext.Net.Multiplexing;

using Runtime.CompilerServices;
using Threading;

public abstract partial class MultiplexingListener
{
    private readonly Channel<ServerStream> pendingStreams;
    private readonly PipeOptions options;
    private readonly int fragmentSize;

    protected abstract Socket Listen();
    
    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task ListenAsync(CancellationToken token)
    {
        var connections = new HashSet<Task>();
        var listeningSocket = Listen();
        try
        {
            while (!token.IsCancellationRequested)
            {
                var clientSocket = await listeningSocket.AcceptAsync(token).ConfigureAwait(false);
                connections.Add(DispatchAsync(clientSocket, token));

                // GC: remove completed tasks
                connections.RemoveWhere(static t => t.IsCompleted);
            }
        }
        finally
        {
            listeningSocket.Dispose();
            await Task.WhenAll(connections).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            connections.Clear();
        }
    }

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task DispatchAsync(Socket socket, CancellationToken token)
    {
        var receiveLoop = Task.CompletedTask;
        var streamsToRemove = new List<ulong>();
        var streams = new ConcurrentDictionary<ulong, StreamHandler>();
        using var writeSignal = new AsyncAutoResetEvent(initialState: false);
        using var sendBuffer = options.Pool.Rent(fragmentSize);
        
        // send loop
        while (!token.IsCancellationRequested)
        {
            try
            {
                // send data
                foreach (var (streamId, stream) in streams)
                {
                    if (stream.IsClosed)
                    {
                        await stream.Input.CompleteAsync().ConfigureAwait(false);
                        streamsToRemove.Add(streamId);
                        continue;
                    }

                    ReadResult readResult;
                    try
                    {
                        if (!stream.Input.TryRead(out readResult))
                            continue;
                    }
                    catch (Exception e)
                    {
                        await stream.Input.CompleteAsync(e).ConfigureAwait(false);
                        stream.Output.CancelPendingFlush();
                        await stream.Output.CompleteAsync(e).ConfigureAwait(false);
                        streamsToRemove.Add(streamId);
                        continue;
                    }

                    // write fragment
                    var isCompleted = readResult.IsCompleted;
                    await socket.SendAsync(
                        StreamHandler.PrepareFragment(sendBuffer.Memory, writeSignal, streamId, stream.Input, readResult.Buffer, ref isCompleted),
                        token).ConfigureAwait(false);

                    if (isCompleted)
                    {
                        await stream.Input.CompleteAsync().ConfigureAwait(false);
                    }
                }

                // remove completed streams
                foreach (var streamIdToRemove in streamsToRemove)
                {
                    streams.TryRemove(streamIdToRemove, out _);
                }

                // wait for input data
                await writeSignal.WaitAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException e) when (e.CancellationToken == token)
            {
                await receiveLoop.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                await StreamHandler.CompleteAllAsync(streams, streamsToRemove, e).ConfigureAwait(false);
                break;
            }
            catch (SocketException e)
            {
                await receiveLoop.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                await StreamHandler.CompleteAllAsync(streams, streamsToRemove, e).ConfigureAwait(false);
                break;
            }
            finally
            {
                streamsToRemove.Clear();
            }
        }

        try
        {
            socket.Disconnect(reuseSocket: false);
        }
        finally
        {
            socket.Dispose();
        }
    }
}
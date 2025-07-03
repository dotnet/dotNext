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
    private readonly Channel<StreamHandler> pendingStreams;
    private readonly PipeOptions options;
    private readonly int fragmentSize;
    private readonly Func<ulong, AsyncAutoResetEvent, StreamHandler> streamFactory;

    protected abstract Socket Listen();

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task ListenAsync(CancellationToken token)
    {
        var connections = new HashSet<WeakReference<Task>>();
        var listeningSocket = Listen();
        try
        {
            while (!token.IsCancellationRequested)
            {
                var clientSocket = await listeningSocket.AcceptAsync(token).ConfigureAwait(false);
                connections.Add(new(DispatchAsync(clientSocket, token)));

                // GC: remove completed tasks
                connections.RemoveWhere(IsCompleted);
            }
        }
        finally
        {
            listeningSocket.Dispose();
            await Task.WhenAll(connections.Select(Unwrap)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            connections.Clear();
        }

        static bool IsCompleted(WeakReference<Task> taskRef)
            => !taskRef.TryGetTarget(out var task) || task.IsCompleted;

        static Task Unwrap(WeakReference<Task> taskRef)
        {
            if (!taskRef.TryGetTarget(out var task))
                task = Task.CompletedTask;

            return task;
        }
    }

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task ReceiveAsync(Socket socket, CancellationToken token)
    {
        var streams = new ConcurrentDictionary<ulong, StreamHandler>();
        var receiveBuffer = options.Pool.Rent(fragmentSize);
        var writeSignal = new AsyncAutoResetEvent(initialState: false);

        try
        {
            for (var totalBytes = 0; !token.IsCancellationRequested;)
            {
                // read at least header
                while (totalBytes < FragmentHeader.Size)
                {
                    totalBytes += await socket.ReceiveAsync(receiveBuffer.Memory.Slice(totalBytes), token).ConfigureAwait(false);
                }

                var header = FragmentHeader.Parse(receiveBuffer.Memory.Span);
                totalBytes -= FragmentHeader.Size;

                // read the fragment
                while (totalBytes < header.Length)
                {
                    totalBytes += await socket.ReceiveAsync(receiveBuffer.Memory.Slice(totalBytes), token).ConfigureAwait(false);
                }

                var stream = streams.GetOrAdd(header.Id, streamFactory, writeSignal);
                
                // write the fragment to the pipe
                FlushResult result;
                try
                {
                    result = await stream.Output.WriteAsync(
                        receiveBuffer.Memory.Slice(0, header.Length),
                        token).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    await stream.Output.CompleteAsync(e).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            receiveBuffer.Dispose();
            writeSignal.Dispose();
        }
    }

    private StreamHandler CreateHandler(ulong streamId, AsyncAutoResetEvent writeSignal)
        => new(options, writeSignal);

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task DispatchAsync(Socket socket, CancellationToken token)
    {
        var streamsToRemove = new List<ulong>();
        var streams = new ConcurrentDictionary<ulong, StreamHandler>();
        using var writeSignal = new AsyncAutoResetEvent(initialState: false);
        using var sendBuffer = options.Pool.Rent(fragmentSize);
        var receiveLoop = ReceiveAsync(socket, token);
        
        // send loop
        while (!token.IsCancellationRequested)
        {
            // rethrow exception from the receiving loop
            if (receiveLoop.IsCompleted)
                await receiveLoop.ConfigureAwait(false);
            
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
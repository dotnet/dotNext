using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace DotNext.Net.Multiplexing;

using Runtime.CompilerServices;
using Threading;

partial class MultiplexedClient
{
    private readonly ConcurrentDictionary<ulong, StreamHandler> streams;
    private readonly byte[] sendBuffer, receiveBuffer;
    private readonly AsyncAutoResetEvent writeSignal;
    private readonly PipeOptions options;
    private readonly Task dispatcher;
    private ulong streamId;

    private async Task ReceiveAsync(Socket socket, CancellationToken token)
    {
        try
        {
            for (var totalBytes = 0; !token.IsCancellationRequested;)
            {
                // read at least header
                while (totalBytes < FragmentHeader.Size)
                {
                    totalBytes += await socket.ReceiveAsync(receiveBuffer.AsMemory(totalBytes), token).ConfigureAwait(false);
                }

                var header = FragmentHeader.Parse(receiveBuffer);
                totalBytes -= FragmentHeader.Size;

                // read the fragment
                while (totalBytes < header.Length)
                {
                    totalBytes += await socket.ReceiveAsync(receiveBuffer.AsMemory(totalBytes), token).ConfigureAwait(false);
                }

                // write the fragment to the pipe
                switch (header.Control)
                {
                    case FragmentControl.FinalDataChunk when streams.TryRemove(header.Id, out var stream):
                    case FragmentControl.DataChunk when streams.TryGetValue(header.Id, out stream):
                        FlushResult result;
                        try
                        {
                            result = await stream.Output.WriteAsync(
                                receiveBuffer.AsMemory(0, header.Length),
                                token).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            await stream.Output.CompleteAsync(e).ConfigureAwait(false);
                            goto default;
                        }

                        if (result.IsCanceled)
                        {
                            stream.Close();
                        }
                        else if (result.IsCompleted || header.Control is FragmentControl.FinalDataChunk)
                        {
                            stream.Close();
                            await stream.Output.CompleteAsync().ConfigureAwait(false);
                        }

                        goto default;
                    default:
                        totalBytes -= header.Length;
                        receiveBuffer
                            .AsSpan(header.Length, totalBytes)
                            .CopyTo(receiveBuffer.AsSpan(0, totalBytes));
                        break;
                }
            }
        }
        catch
        {
            writeSignal.Set();
            throw;
        }
    }
    
    // Dispatcher consists of two parallel tasks:
    // 1. Sending task (sender)
    // 2. Receiving task (receiver)
    //
    // Sender can complete its PipeWriter, which means that the sender doesn't expect to write more data.
    // After that, the sender needs to wait for PipeReader to be completed. However, PipeReader can
    // be completed by the server because there is no more data expected from it. In that case,
    // sender's PipeReader completes as well.
    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task DispatchAsync(CancellationToken token)
    {
        var socket = default(Socket);
        var receiveLoop = Task.CompletedTask;
        var streamsToRemove = new List<ulong>();
        
        // send loop
        while (!token.IsCancellationRequested)
        {
            try
            {
                // rethrow exception from the receiving loop
                if (receiveLoop.IsCompleted)
                    await receiveLoop.ConfigureAwait(false);
                
                // connect if needed
                if (socket is null)
                {
                    socket = await ConnectAsync(token).ConfigureAwait(false);
                    receiveLoop = ReceiveAsync(socket, token);
                }

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
                    await socket.SendAsync(PrepareFragment(streamId, stream.Input, readResult.Buffer, ref isCompleted), token).ConfigureAwait(false);

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
                Debug.Assert(socket is not null);
                
                socket.Dispose();
                socket = null;
                await receiveLoop.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                await StreamHandler.CompleteAllAsync(streams, streamsToRemove, e).ConfigureAwait(false);
            }
            finally
            {
                streamsToRemove.Clear();
            }
        }

        if (socket is not null)
        {
            socket.Disconnect(reuseSocket: false);
            socket.Dispose();
        }
    }

    private ReadOnlyMemory<byte> PrepareFragment(ulong streamId, PipeReader reader, ReadOnlySequence<byte> inputBuffer, ref bool isCompleted)
        => StreamHandler.PrepareFragment(sendBuffer, writeSignal, streamId, reader, inputBuffer, ref isCompleted);
}
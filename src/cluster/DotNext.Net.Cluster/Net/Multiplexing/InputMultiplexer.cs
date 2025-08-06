using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.Sockets;
using DotNext.Buffers;

namespace DotNext.Net.Multiplexing;

using Threading;

internal sealed class InputMultiplexer(
    ConcurrentDictionary<ulong, StreamHandler> streams,
    AsyncAutoResetEvent writeSignal,
    Memory<byte> buffer,
    TimeSpan timeout,
    TimeSpan heartbeatTimeout,
    CancellationToken token) : Multiplexer(streams, new ConcurrentQueue<ProtocolCommand>(), token)
{
    public OutputMultiplexer CreateOutput(Memory<byte> outputBuffer, TimeSpan receiveTimeout)
        => new(streams, writeSignal, commands, outputBuffer, receiveTimeout, token);

    public OutputMultiplexer CreateOutput(Memory<byte> outputBuffer, TimeSpan receiveTimeout, Func<StreamHandler?> handlerFactory, CancellationToken token)
        => new(streams, writeSignal, commands, outputBuffer, receiveTimeout, token) { HandlerFactory = handlerFactory };

    private ValueTask ReadAsync(
        ulong streamId,
        StreamHandler stream,
        out ReadResult? result)
    {
        var task = ValueTask.CompletedTask;
        result = null;

        if (!stream.IsTransportInputCompleted(out var appSideCompleted))
        {
            try
            {
                if (stream.Input.TryRead(out var readResult))
                    result = readResult;
            }
            catch (Exception e)
            {
                task = stream.CompleteTransportInputAsync(e);
            }
        }
        else if (appSideCompleted && streams.As<ICollection<KeyValuePair<ulong, StreamHandler>>>().Remove(new(streamId, stream)))
        {
            stream.Output.CancelPendingFlush();
            task = stream.CompleteTransportOutputAsync();

            commands.TryAdd(new StreamClosedCommand(streamId));
        }

        return task;
    }

    public async ValueTask ProcessAsync(Func<bool> condition, Socket socket)
    {
        // send data
        using var enumerator = streams.GetEnumerator();
        for (ReadOnlyMemory<byte> dataToSend; !token.IsCancellationRequested && condition(); enumerator.Reset())
        {
            while (enumerator.MoveNext())
            {
                var (streamId, stream) = enumerator.Current;
                await ReadAsync(streamId, stream, out var readResult).ConfigureAwait(false);

                if (readResult is null)
                    continue;

                // write fragment
                dataToSend = PrepareFragment(buffer, streamId, in Nullable.GetValueRefOrDefaultRef(in readResult), out var completed, out var position);

                Debug.Assert(dataToSend.Length >= FragmentHeader.Size);
                timeoutSource.Start(timeout);
                try
                {
                    do
                    {
                        dataToSend.Advance(await socket.SendAsync(dataToSend, timeoutSource.Token).ConfigureAwait(false));
                    } while (!dataToSend.IsEmpty);
                }
                catch (OperationCanceledException e) when (timeoutSource.IsCanceled(e))
                {
                    throw new OperationCanceledException(ExceptionMessages.ConnectionClosed, e, token);
                }
                catch (OperationCanceledException e) when (timeoutSource.IsTimedOut(e))
                {
                    throw new TimeoutException(ExceptionMessages.ConnectionTimedOut, e);
                }
                finally
                {
                    stream.Input.AdvanceTo(position);
                    await timeoutSource.ResetAsync(token).ConfigureAwait(false);
                }

                if (completed)
                {
                    await stream.CompleteTransportInputAsync().ConfigureAwait(false);
                }
                else
                {
                    writeSignal.Set();
                }
            }

            // wait for input data
            if (!await writeSignal.WaitAsync(heartbeatTimeout, token).ConfigureAwait(false))
            {
                // send the heartbeat
                commands.TryAdd(HeartbeatCommand.Instance);
            }

            // process protocol commands
            while (commands.TryTake(out var command))
            {
                timeoutSource.Start(timeout);
                try
                {
                    for (dataToSend = buffer.Slice(0, command.Write(buffer.Span)); !dataToSend.IsEmpty;)
                    {
                        dataToSend.Advance(await socket.SendAsync(dataToSend, timeoutSource.Token).ConfigureAwait(false));
                    }
                }
                catch (OperationCanceledException e) when (timeoutSource.IsTimedOut(e))
                {
                    throw new TimeoutException(ExceptionMessages.ConnectionTimedOut, e);
                }
                catch (OperationCanceledException e) when (timeoutSource.IsCanceled(e))
                {
                    throw new OperationCanceledException(ExceptionMessages.ConnectionClosed, e, token);
                }
                finally
                {
                    await timeoutSource.ResetAsync(token).ConfigureAwait(false);
                }
            }
        }
    }

    private static ReadOnlyMemory<byte> PrepareFragment(Memory<byte> sendBuffer, ulong streamId,
        in ReadResult result, out bool isCompleted, out SequencePosition position)
    {
        var inputBuffer = result.Buffer;
        inputBuffer.CopyTo(sendBuffer.Span.Slice(FragmentHeader.Size), out var writtenCount);
        Debug.Assert(writtenCount <= ushort.MaxValue);
        
        position = inputBuffer.GetPosition(writtenCount);

        FragmentControl control;
        if (!position.Equals(inputBuffer.End))
        {
            control = FragmentControl.DataChunk;
            isCompleted = false;
        }
        else if (isCompleted = result.IsCompleted)
        {
            control = FragmentControl.FinalDataChunk;
        }
        else
        {
            control = FragmentControl.DataChunk;
        }

        new FragmentHeader(streamId, control, (ushort)writtenCount).Format(sendBuffer.Span);
        return sendBuffer.Slice(0, writtenCount + FragmentHeader.Size);
    }

    public async ValueTask CompleteAllAsync(Exception e)
    {
        foreach (var id in streams.Keys)
        {
            if (streams.TryRemove(id, out var stream))
            {
                await stream.CompleteTransportOutputAsync(e).ConfigureAwait(false);
                await stream.CompleteTransportInputAsync(e).ConfigureAwait(false);
            }
        }
    }
}

file static class MemoryExtensions
{
    public static void Advance(this ref ReadOnlyMemory<byte> buffer, int count)
        => buffer = buffer.Slice(count);
}
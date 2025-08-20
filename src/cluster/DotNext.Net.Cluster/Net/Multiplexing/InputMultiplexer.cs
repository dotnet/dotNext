using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace DotNext.Net.Multiplexing;

using Buffers;
using Threading;

internal sealed class InputMultiplexer(
    ConcurrentDictionary<uint, MultiplexedStream> streams,
    AsyncAutoResetEvent writeSignal,
    BufferWriter<byte> framingBuffer,
    int flushThreshold,
    UpDownCounter<int> streamCounter,
    in TagList measurementTags,
    TimeSpan timeout,
    TimeSpan heartbeatTimeout,
    CancellationToken token) : Multiplexer(streams, new ConcurrentQueue<ProtocolCommand>(), streamCounter, measurementTags, token)
{
    
    public TimeSpan Timeout => timeout;

    public bool TryAddStream(uint streamId, MultiplexedStream stream)
    {
        var result = streams.TryAdd(streamId, stream);
        ChangeStreamCount(Unsafe.BitCast<bool, byte>(result));
        return result;
    }

    public bool TryRemoveStream(uint streamId, MultiplexedStream stream)
    {
        var removed = streams.TryRemove(new(streamId, stream));
        ChangeStreamCount(-Unsafe.BitCast<bool, byte>(removed));
        return removed;
    }

    public OutputMultiplexer CreateOutput(Memory<byte> framingBuffer, TimeSpan receiveTimeout)
        => new(streams, writeSignal, commands, framingBuffer, streamCounter, measurementTags, receiveTimeout, RootToken);

    public OutputMultiplexer CreateOutput(Memory<byte> framingBuffer, TimeSpan receiveTimeout, Func<AsyncAutoResetEvent, MultiplexedStream?> handlerFactory,
        CancellationToken token)
        => new(streams, writeSignal, commands, framingBuffer, streamCounter, measurementTags, receiveTimeout, token)
            { HandlerFactory = handlerFactory };

    public async Task ProcessAsync(Func<bool> condition, Socket socket)
    {
        using var enumerator = streams.GetEnumerator();
        for (var requiresHeartbeat = false;
             condition();
             requiresHeartbeat = !await writeSignal.WaitAsync(heartbeatTimeout, RootToken).ConfigureAwait(false))
        {
            framingBuffer.Clear(reuseBuffer: true);

            // combine streams
            while (enumerator.MoveNext())
            {
                var (streamId, stream) = enumerator.Current;

                if (stream.IsCompleted && TryRemoveStream(streamId, stream))
                {
                    Protocol.WriteStreamClosed(framingBuffer, streamId);
                }
                else
                {
                    await stream.WriteFrameAsync(framingBuffer, streamId).ConfigureAwait(false);
                }

                // write the buffer on overflow
                if (framingBuffer.WrittenCount >= flushThreshold)
                {
                    await SendAsync(framingBuffer.WrittenMemory, socket).ConfigureAwait(false);
                    framingBuffer.Clear(reuseBuffer: true);
                }
            }

            // process protocol commands
            commands.Serialize(framingBuffer);

            switch (framingBuffer.WrittenCount)
            {
                case 0 when requiresHeartbeat:
                    Protocol.WriteHeartbeat(framingBuffer);
                    goto default;
                case 0:
                    break;
                default:
                    await SendAsync(framingBuffer.WrittenMemory, socket).ConfigureAwait(false);
                    break;
            }

            enumerator.Reset();
        }
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask SendAsync(ReadOnlyMemory<byte> buffer, Socket socket)
    {
        for (int bytesWritten; !buffer.IsEmpty; buffer = buffer.Slice(bytesWritten))
        {
            StartOperation(timeout);
            try
            {
                bytesWritten = await socket.SendAsync(buffer, TimeBoundedToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException e) when (IsOperationTimedOut(e))
            {
                throw new TimeoutException(ExceptionMessages.ConnectionTimedOut, e);
            }
            catch (OperationCanceledException e) when (IsOperationCanceled(e))
            {
                throw new OperationCanceledException(ExceptionMessages.ConnectionClosed, e, RootToken);
            }
            finally
            {
                await ResetOperationTimeoutAsync().ConfigureAwait(false);
            }
        }
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
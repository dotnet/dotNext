using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace DotNext.Net.Multiplexing;

using Buffers;
using Threading;

internal sealed class InputMultiplexer<T>() : Multiplexer<T>(new(), new ConcurrentQueue<ProtocolCommand>())
    where T : IStreamMetrics
{
    public required TimeSpan Timeout { get; init; }

    public required TimeSpan HeartbeatTimeout { private get; init; }

    public required int FlushThreshold { private get; init; }

    public required BufferWriter<byte> FramingBuffer { private get; init; }

    public required AsyncAutoResetEvent TransportSignal { private get; init; }

    public bool TryAddStream(uint streamId, MultiplexedStream stream)
    {
        var result = Streams.TryAdd(streamId, stream);
        ChangeStreamCount(Unsafe.BitCast<bool, byte>(result));
        return result;
    }

    private bool TryRemoveStream(uint streamId, MultiplexedStream stream)
    {
        var removed = Streams.TryRemove(new(streamId, stream));
        ChangeStreamCount(-Unsafe.BitCast<bool, byte>(removed));
        return removed;
    }

    public OutputMultiplexer<T> CreateOutput(Memory<byte> framingBuffer, TimeSpan receiveTimeout) => new(Streams, Commands)
    {
        MeasurementTags = MeasurementTags,
        RootToken = RootToken,
        TokenMultiplexer = TokenMultiplexer,
        FramingBuffer = framingBuffer,
        Timeout = receiveTimeout,
        TransportSignal = TransportSignal,
    };

    public OutputMultiplexer<T> CreateOutput(Memory<byte> framingBuffer, TimeSpan receiveTimeout, MultiplexedStreamFactory handlerFactory,
        CancellationToken token) => new(Streams, Commands)
    {
        MeasurementTags = MeasurementTags,
        RootToken = token,
        TokenMultiplexer = TokenMultiplexer,
        FramingBuffer = framingBuffer,
        Timeout = receiveTimeout,
        TransportSignal = TransportSignal,
        Factory = handlerFactory,
    };

    public async Task ProcessAsync(Func<bool> condition, Socket socket)
    {
        using var enumerator = Streams.GetEnumerator();
        for (var requiresHeartbeat = false;
             condition();
             requiresHeartbeat = !await TransportSignal.WaitAsync(HeartbeatTimeout, RootToken).ConfigureAwait(false))
        {
            FramingBuffer.Clear(reuseBuffer: true);

            // combine streams
            while (enumerator.MoveNext())
            {
                var (streamId, stream) = enumerator.Current;

                if (stream.IsCompleted && TryRemoveStream(streamId, stream))
                {
                    Protocol.WriteStreamClosed(FramingBuffer, streamId);
                }
                else
                {
                    await stream.WriteFrameAsync(FramingBuffer, streamId).ConfigureAwait(false);
                }

                // write the buffer on overflow
                if (FramingBuffer.WrittenCount >= FlushThreshold)
                {
                    await SendAsync(FramingBuffer.WrittenMemory, socket).ConfigureAwait(false);
                    FramingBuffer.Clear(reuseBuffer: true);
                }
            }

            // process protocol commands
            Commands.Serialize(FramingBuffer);

            switch (FramingBuffer.WrittenCount)
            {
                case 0 when requiresHeartbeat:
                    Protocol.WriteHeartbeat(FramingBuffer);
                    goto default;
                case 0:
                    break;
                default:
                    await SendAsync(FramingBuffer.WrittenMemory, socket).ConfigureAwait(false);
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
            var timeoutScope = TokenMultiplexer.Combine(Timeout, RootToken);
            try
            {
                bytesWritten = await socket.SendAsync(buffer, timeoutScope.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException e) when (e.CausedByTimeout(timeoutScope))
            {
                throw new TimeoutException(ExceptionMessages.ConnectionTimedOut, e);
            }
            catch (OperationCanceledException e) when (e.CausedBy(timeoutScope, RootToken))
            {
                throw new OperationCanceledException(ExceptionMessages.ConnectionClosed, e, RootToken);
            }
            finally
            {
                await timeoutScope.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public async ValueTask CompleteAllAsync(Exception e)
    {
        foreach (var id in Streams.Keys)
        {
            if (Streams.TryRemove(id, out var stream))
            {
                await stream.CompleteTransportOutputAsync(e).ConfigureAwait(false);
                await stream.CompleteTransportInputAsync(e).ConfigureAwait(false);
            }
        }
    }
}
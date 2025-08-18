using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace DotNext.Net.Multiplexing;

using Buffers;
using Threading;

internal sealed class InputMultiplexer(
    ConcurrentDictionary<ulong, MultiplexedStream> streams,
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

    public bool TryAddStream(ulong streamId, MultiplexedStream stream)
    {
        var result = streams.TryAdd(streamId, stream);
        ChangeStreamCount(Unsafe.BitCast<bool, byte>(result));
        return result;
    }
    
    public OutputMultiplexer CreateOutput(Memory<byte> framingBuffer, TimeSpan receiveTimeout)
        => new(streams, writeSignal, commands, framingBuffer, streamCounter, measurementTags, receiveTimeout, Token);

    public OutputMultiplexer CreateOutput(Memory<byte> framingBuffer, TimeSpan receiveTimeout, Func<AsyncAutoResetEvent, MultiplexedStream?> handlerFactory,
        CancellationToken token)
        => new(streams, writeSignal, commands, framingBuffer, streamCounter, measurementTags, receiveTimeout, token)
            { HandlerFactory = handlerFactory };

    public async Task ProcessAsync(Func<bool> condition, Socket socket)
    {
        using var enumerator = streams.GetEnumerator();
        for (var requiresHeartbeat = false;
             condition();
             requiresHeartbeat = !await writeSignal.WaitAsync(heartbeatTimeout, Token).ConfigureAwait(false))
        {
            framingBuffer.Clear(reuseBuffer: true);

            // combine streams
            while (enumerator.MoveNext())
            {
                var (streamId, stream) = enumerator.Current;

                if (stream.IsCompleted && streams.TryRemove(streamId, out _))
                {
                    Protocol.WriteStreamClosed(framingBuffer, streamId);
                    ChangeStreamCount(-1);
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
            timeoutSource.Start(timeout);
            try
            {
                bytesWritten = await socket.SendAsync(buffer, timeoutSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException e) when (timeoutSource.IsTimedOut(e))
            {
                throw new TimeoutException(ExceptionMessages.ConnectionTimedOut, e);
            }
            catch (OperationCanceledException e) when (timeoutSource.IsCanceled(e))
            {
                throw new OperationCanceledException(ExceptionMessages.ConnectionClosed, e, Token);
            }
            finally
            {
                await timeoutSource.ResetAsync(Token).ConfigureAwait(false);
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
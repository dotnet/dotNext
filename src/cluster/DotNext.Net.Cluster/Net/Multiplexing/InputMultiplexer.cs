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
        while (!Token.IsCancellationRequested && condition())
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
            }

            // process protocol commands
            while (commands.TryTake(out var command))
            {
                command.Write(framingBuffer);
            }

            if (framingBuffer.WrittenCount is 0)
            {
                Protocol.WriteHeartbeat(framingBuffer);
            }

            // send combined buffer
            var bufferToSend = framingBuffer.WrittenMemory;
            for (int bytesWritten; !bufferToSend.IsEmpty; bufferToSend = bufferToSend.Slice(bytesWritten))
            {
                timeoutSource.Start(timeout);
                try
                {
                    bytesWritten = await socket.SendAsync(bufferToSend, SocketFlags.None).ConfigureAwait(false);
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
            
            enumerator.Reset();

            // wait for input data
            await writeSignal.WaitAsync(heartbeatTimeout, Token).ConfigureAwait(false);
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
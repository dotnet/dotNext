using System.Collections.Concurrent;
using System.Net.Sockets;

namespace DotNext.Net.Multiplexing;

using Threading;

internal sealed class OutputMultiplexer<T>(
    ConcurrentDictionary<uint, MultiplexedStream> streams,
    IProducerConsumerCollection<ProtocolCommand> commands): Multiplexer<T>(streams, commands)
    where T : IStreamMetrics
{
    private readonly Memory<byte> framingBuffer;
    private readonly MultiplexedStreamFactory? factory;
    
    public required AsyncAutoResetEvent TransportSignal { private get; init; }

    public required Memory<byte> FramingBuffer
    {
        init => framingBuffer = value;
    }

    public required TimeSpan Timeout { private get; init; }

    public MultiplexedStreamFactory Factory
    {
        init => factory = value;
    }

    public Task ProcessAsync(Socket socket)
    {
        var task = ProcessCoreAsync(socket);
        
        // if output multiplexer is completed due to exception, we need to trigger
        // the input multiplexer to handle the error
        task.ConfigureAwait(false).GetAwaiter().UnsafeOnCompleted(TransportSignal.SetNoResult);
        return task;
    }

    private async Task ProcessCoreAsync(Socket socket)
    {
        FrameHeader header;
        for (var bufferedBytes = 0;; AdjustFramingBuffer(ref bufferedBytes, header, framingBuffer.Span))
        {
            StartOperation(Timeout); // resumed by heartbeat
            try
            {
                // read at least header
                while (bufferedBytes < FrameHeader.Size)
                {
                    bufferedBytes += await socket.ReceiveAsync(framingBuffer.Slice(bufferedBytes), TimeBoundedToken).ConfigureAwait(false);
                }

                header = FrameHeader.Parse(framingBuffer.Span);

                // read the fragment
                while (bufferedBytes < header.Length + FrameHeader.Size)
                {
                    bufferedBytes += await socket.ReceiveAsync(framingBuffer.Slice(bufferedBytes), TimeBoundedToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException e) when (IsOperationCanceled(e))
            {
                throw new OperationCanceledException(ExceptionMessages.ConnectionClosed, e, RootToken);
            }
            catch (OperationCanceledException e) when (IsOperationTimedOut(e))
            {
                throw new TimeoutException(ExceptionMessages.ConnectionTimedOut, e);
            }
            finally
            {
                await ResetOperationTimeoutAsync().ConfigureAwait(false);
            }

            await ReadFrameAsync(header).ConfigureAwait(false);
        }
    }

    private ValueTask ReadFrameAsync(FrameHeader header)
        => GetOrCreateStream(header) is { } stream
            ? stream.ReadFrameAsync(header.Control, framingBuffer.Slice(FrameHeader.Size, header.Length), RootToken)
            : ValueTask.CompletedTask;

    private MultiplexedStream? GetOrCreateStream(FrameHeader header)
    {
        if (header.Control is FrameControl.Heartbeat)
            return null;

        if (Streams.TryGetValue(header.Id, out var stream))
            return stream.IsTransportOutputCompleted ? null : stream;

        if (factory is null || header.CanBeIgnored)
            return null;
        
        if ((stream = factory(TransportSignal, in MeasurementTags)) is null)
        {
            Commands.TryAdd(new StreamRejectedCommand(header.Id));
            TransportSignal.Set();
        }
        else
        {
            Streams[header.Id] = stream;
            ChangeStreamCount();
        }

        return stream;
    }

    private static void AdjustFramingBuffer(ref int bufferedBytes, FrameHeader header, Span<byte> framingBuffer)
    {
        var bytesWritten = header.Length + FrameHeader.Size;
        framingBuffer
            .Slice(bytesWritten, bufferedBytes -= bytesWritten)
            .CopyTo(framingBuffer);
    }
}

file static class AsyncAutoResetEventExtensions
{
    public static void SetNoResult(this AsyncAutoResetEvent resetEvent) => resetEvent.Set();
}
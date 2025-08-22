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

            MultiplexedStream? stream;
            if (header.Control is FrameControl.Heartbeat)
            {
                continue;
            }
            else if (Streams.TryGetValue(header.Id, out stream))
            {
                if (stream.IsTransportOutputCompleted)
                    continue;
            }
            else if (factory is null || header.CanBeIgnored)
            {
                continue;
            }
            else if ((stream = factory(TransportSignal, in MeasurementTags)) is null)
            {
                Commands.TryAdd(new StreamRejectedCommand(header.Id));
                TransportSignal.Set();
                continue;
            }
            else
            {
                Streams[header.Id] = stream;
                ChangeStreamCount();
            }

            // write the frame to the output header
            await stream
                .ReadFrameAsync(header.Control, framingBuffer.Slice(FrameHeader.Size, header.Length), RootToken)
                .ConfigureAwait(false);
        }
    }

    private static void AdjustFramingBuffer(ref int bufferedBytes, in FrameHeader header, Span<byte> framingBuffer)
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
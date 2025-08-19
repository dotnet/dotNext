using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Sockets;

namespace DotNext.Net.Multiplexing;

using Threading;

internal sealed class OutputMultiplexer(
    ConcurrentDictionary<ulong, MultiplexedStream> streams,
    AsyncAutoResetEvent writeSignal,
    IProducerConsumerCollection<ProtocolCommand> commands,
    Memory<byte> framingBuffer,
    UpDownCounter<int> streamCounter,
    in TagList measurementTags,
    TimeSpan timeout,
    CancellationToken token)
    : Multiplexer(streams, commands, streamCounter, measurementTags, token)
{
    public Func<AsyncAutoResetEvent, MultiplexedStream?>? HandlerFactory { get; init; }

    public Task ProcessAsync(Socket socket)
    {
        var task = ProcessCoreAsync(socket);
        
        // if output multiplexer is completed due to exception, we need to trigger
        // the input multiplexer to handle the error
        task.ConfigureAwait(false).GetAwaiter().UnsafeOnCompleted(writeSignal.SetNoResult);
        return task;
    }

    private async Task ProcessCoreAsync(Socket socket)
    {
        FrameHeader header;
        for (var bufferedBytes = 0;; AdjustFramingBuffer(ref bufferedBytes, header, framingBuffer.Span))
        {
            StartOperation(timeout); // resumed by heartbeat
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

            if (header.Control is FrameControl.Heartbeat)
                continue;

            if (!streams.TryGetValue(header.Id, out var stream))
            {
                if (HandlerFactory is null || header.CanBeIgnored)
                {
                    continue;
                }

                if ((stream = HandlerFactory(writeSignal)) is null)
                {
                    commands.TryAdd(new StreamRejectedCommand(header.Id));
                    writeSignal.Set();
                    continue;
                }

                streams[header.Id] = stream;
                ChangeStreamCount();
            }
            else if (stream.IsTransportOutputCompleted)
            {
                continue;
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
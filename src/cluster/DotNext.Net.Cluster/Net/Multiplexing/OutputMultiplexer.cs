using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO.Pipelines;
using System.Net.Sockets;

namespace DotNext.Net.Multiplexing;

using Threading;

internal sealed class OutputMultiplexer(
    ConcurrentDictionary<ulong, MultiplexedStream> streams,
    AsyncAutoResetEvent writeSignal,
    IProducerConsumerCollection<ProtocolCommand> commands,
    Memory<byte> buffer,
    UpDownCounter<int> streamCounter,
    in TagList measurementTags,
    TimeSpan timeout,
    CancellationToken token) : Multiplexer(streams, commands, streamCounter, measurementTags, token)
{
    public Func<MultiplexedStream?>? HandlerFactory { get; init; }

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
        FragmentHeader header;
        for (var totalBytes = 0; !Token.IsCancellationRequested; AdjustReceiveBuffer(header.Length, ref totalBytes, buffer.Span))
        {
            timeoutSource.Start(timeout); // resumed by heartbeat
            try
            {
                // read at least header
                while (totalBytes < FragmentHeader.Size)
                {
                    totalBytes += await socket.ReceiveAsync(buffer.Slice(totalBytes), Token).ConfigureAwait(false);
                }

                header = FragmentHeader.Parse(buffer.Span);
                totalBytes -= FragmentHeader.Size;

                // read the fragment
                while (totalBytes < header.Length)
                {
                    totalBytes += await socket.ReceiveAsync(buffer.Slice(totalBytes), timeoutSource.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException e) when (timeoutSource.IsCanceled(e))
            {
                throw new OperationCanceledException(ExceptionMessages.ConnectionClosed, e, Token);
            }
            catch (OperationCanceledException e) when (timeoutSource.IsTimedOut(e))
            {
                throw new TimeoutException(ExceptionMessages.ConnectionTimedOut, e);
            }
            finally
            {
                await timeoutSource.ResetAsync(Token).ConfigureAwait(false);
            }

            switch (header.Control)
            {
                case FragmentControl.Heartbeat:
                    continue;
            }

            if (!streams.TryGetValue(header.Id, out var handler))
            {
                if (HandlerFactory is null || header.CanBeIgnored)
                {
                    continue;
                }

                if ((handler = HandlerFactory()) is null)
                {
                    commands.TryAdd(new StreamRejectedCommand(header.Id));
                    continue;
                }

                streams[header.Id] = handler;
                ChangeStreamCount();
            }
            else if (handler.IsTransportOutputCompleted)
            {
                continue;
            }

            // write the fragment to the output header
            FlushResult result;
            timeoutSource.Start(timeout);
            try
            {
                result = await WriteAsync(header, handler, timeoutSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException e) when (timeoutSource.IsCanceled(e))
            {
                throw new OperationCanceledException(ExceptionMessages.ConnectionClosed, e, Token);
            }
            catch (Exception e)
            {
                // on exception, complete input/output and remove the stream
                await CompleteTransportOutputAsync(handler, e).ConfigureAwait(false);
                result = new(isCanceled: true, isCompleted: false);
            }
            finally
            {
                await timeoutSource.ResetAsync(Token).ConfigureAwait(false);
            }

            if (!result.IsCanceled && (result.IsCompleted || header.Control is FragmentControl.FinalDataChunk))
            {
                await handler.CompleteTransportOutputAsync().ConfigureAwait(false);
            }
        }
    }

    private ValueTask CompleteTransportOutputAsync(MultiplexedStream stream, Exception e)
    {
        switch (e)
        {
            case OperationCanceledException canceledEx when timeoutSource.IsTimedOut(canceledEx):
                e = new TimeoutException(ExceptionMessages.PipeTimedOut, e);
                break;
        }

        return stream.CompleteTransportOutputAsync(e);
    }

    private ValueTask<FlushResult> WriteAsync(FragmentHeader header, MultiplexedStream stream, CancellationToken token)
    {
        ValueTask<FlushResult> task;
        switch (header.Control)
        {
            case FragmentControl.StreamRejected:
                task = ValueTask.FromException<FlushResult>(new StreamRejectedException());
                break;
            case FragmentControl.StreamClosed:
                task = CompleteAsync(stream);
                break;
            default:
                task = stream.Output.WriteAsync(buffer.Slice(FragmentHeader.Size, header.Length), token);
                break;
        }

        return task;
    }
    
    private async ValueTask<FlushResult> CompleteAsync(MultiplexedStream stream)
    {
        await stream.CompleteTransportOutputAsync().ConfigureAwait(false);
        stream.Input.CancelPendingRead();
        writeSignal.Set();
        return new(isCanceled: false, isCompleted: true);
    }

    private static void AdjustReceiveBuffer(int bytesRead, ref int totalBytes, Span<byte> buffer)
    {
        totalBytes -= bytesRead;
        buffer.Slice(bytesRead + FragmentHeader.Size, totalBytes)
            .CopyTo(buffer.Slice(0, totalBytes));
    }
}

file static class AsyncAutoResetEventExtensions
{
    public static void SetNoResult(this AsyncAutoResetEvent resetEvent) => resetEvent.Set();
}
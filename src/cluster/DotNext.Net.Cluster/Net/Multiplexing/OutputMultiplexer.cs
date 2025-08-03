using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net.Sockets;

namespace DotNext.Net.Multiplexing;

using Threading;

internal sealed class OutputMultiplexer(
    ConcurrentDictionary<ulong, StreamHandler> streams,
    AsyncAutoResetEvent writeSignal,
    IProducerConsumerCollection<ProtocolCommand> commands,
    Memory<byte> buffer,
    TimeSpan timeout,
    CancellationToken token) : Multiplexer(streams, commands, token)
{
    public Func<StreamHandler?>? HandlerFactory { get; init; }

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
        for (var totalBytes = 0; !token.IsCancellationRequested; AdjustReceiveBuffer(header.Length, ref totalBytes, buffer.Span))
        {
            timeoutSource.Start(timeout); // resumed by heartbeat
            try
            {
                // read at least header
                while (totalBytes < FragmentHeader.Size)
                {
                    totalBytes += await socket.ReceiveAsync(buffer.Slice(totalBytes), token).ConfigureAwait(false);
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
                throw new OperationCanceledException(ExceptionMessages.ConnectionClosed, e, token);
            }
            catch (OperationCanceledException e) when (timeoutSource.IsTimedOut(e))
            {
                throw new TimeoutException(ExceptionMessages.ConnectionTimedOut, e);
            }
            finally
            {
                await timeoutSource.ResetAsync(token).ConfigureAwait(false);
            }

            if (header.Control is FragmentControl.Heartbeat)
                continue;

            if (!streams.TryGetValue(header.Id, out var handler))
            {
                if (HandlerFactory is null)
                {
                    continue;
                }

                if ((handler = HandlerFactory()) is null)
                {
                    commands.TryAdd(new StreamRejectedCommand(header.Id));
                    continue;
                }

                streams[header.Id] = handler;
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
                if (header.Control is FragmentControl.StreamRejected)
                    throw new StreamRejectedException();

                result = await handler.Output.WriteAsync(
                    buffer.Slice(FragmentHeader.Size, header.Length),
                    timeoutSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException e) when (timeoutSource.IsCanceled(e))
            {
                throw new OperationCanceledException(ExceptionMessages.ConnectionClosed, e, token);
            }
            catch (Exception e)
            {
                // on exception, complete input/output and remove the stream
                if (e is OperationCanceledException canceledEx && timeoutSource.IsTimedOut(canceledEx))
                    e = new TimeoutException(ExceptionMessages.PipeTimedOut, e);

                await handler.CompleteTransportOutputAsync(e).ConfigureAwait(false);
                result = new(isCanceled: true, isCompleted: true);
            }
            finally
            {
                await timeoutSource.ResetAsync(token).ConfigureAwait(false);
            }

            if (!result.IsCanceled && (result.IsCompleted || header.Control is FragmentControl.FinalDataChunk))
            {
                await handler.CompleteTransportOutputAsync().ConfigureAwait(false);
            }
        }
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
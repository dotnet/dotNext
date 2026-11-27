using System.Buffers;
using System.IO.Pipelines;
using DotNext.Buffers;

namespace DotNext.IO.Pipelines;

public static partial class PipeExtensions
{
    /// <summary>
    /// Writes sequence of bytes to the underlying stream asynchronously.
    /// </summary>
    /// <param name="writer">The pipe to write into.</param>
    /// <param name="sequence">The sequence of bytes.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask WriteAsync(this PipeWriter writer, ReadOnlySequence<byte> sequence, CancellationToken token = default)
    {
        return sequence.IsSingleSegment
            ? WriteBlockAsync(writer, sequence.First, token)
            : WriteSegmentsAsync(writer, sequence, token);

        static ValueTask WriteBlockAsync(PipeWriter writer, ReadOnlyMemory<byte> block, CancellationToken token)
        {
            var task = writer.WriteAsync(block, token);
            if (!task.IsCompletedSuccessfully)
                return WaitAsync(task, token);

            try
            {
                task.Result.ThrowIfCancellationRequested(token);
            }
            catch (Exception e)
            {
                return ValueTask.FromException(e);
            }

            return ValueTask.CompletedTask;
        }

        static async ValueTask WriteSegmentsAsync(PipeWriter writer, ReadOnlySequence<byte> sequence, CancellationToken token)
        {
            var flushResult = new FlushResult(false, false);

            for (var position = sequence.Start;
                 !flushResult.IsCompleted && sequence.TryGet(ref position, out var block);
                 flushResult.ThrowIfCancellationRequested(token))
            {
                flushResult = await writer.WriteAsync(block, token).ConfigureAwait(false);
            }
        }

        static async ValueTask WaitAsync(ValueTask<FlushResult> task, CancellationToken token)
        {
            var flushResult = await task.ConfigureAwait(false);
            flushResult.ThrowIfCancellationRequested(token);
        }
    }

    /// <summary>
    /// Copies the specified number of bytes from source stream.
    /// </summary>
    /// <param name="destination">The pipe to write into.</param>
    /// <param name="source">The source stream.</param>
    /// <param name="count">The number of bytes to be copied.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="EndOfStreamException"><paramref name="source"/> has not enough data to read.</exception>
    public static async ValueTask CopyFromAsync(this PipeWriter destination, Stream source, long count, CancellationToken token = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentNullException.ThrowIfNull(source);

        for (int bytesRead; count > 0L; count -= bytesRead)
        {
            var buffer = destination.GetMemory() % int.CreateSaturating(count);
            bytesRead = await source.ReadAsync(buffer, token).ConfigureAwait(false);

            if (bytesRead <= 0)
                throw new EndOfStreamException();

            destination.Advance(bytesRead);
            var result = await destination.FlushAsync(token).ConfigureAwait(false);
            result.ThrowIfCancellationRequested(token);

            if (result.IsCompleted)
                break;
        }
    }
}
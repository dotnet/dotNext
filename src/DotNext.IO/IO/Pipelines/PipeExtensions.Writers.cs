using System.Buffers;
using System.IO.Pipelines;

namespace DotNext.IO.Pipelines;

public static partial class PipeExtensions
{
    /// <summary>
    /// Writes sequence of bytes to the underlying stream asynchronously.
    /// </summary>
    /// <param name="writer">The pipe to write into.</param>
    /// <param name="sequence">The sequence of bytes.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The actual number of bytes written to the pipe.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask WriteAsync(this PipeWriter writer, ReadOnlySequence<byte> sequence, CancellationToken token = default)
    {
        var flushResult = new FlushResult(false, false);

        for (var position = sequence.Start; !flushResult.IsCompleted && sequence.TryGet(ref position, out var block); flushResult.ThrowIfCancellationRequested(token))
        {
            flushResult = await writer.WriteAsync(block, token).ConfigureAwait(false);
        }
    }
}
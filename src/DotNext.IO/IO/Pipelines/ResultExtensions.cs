using System.IO.Pipelines;
using System.Runtime.CompilerServices;

namespace DotNext.IO.Pipelines;

/// <summary>
/// Provides various extension methods for <see cref="FlushResult"/>
/// and <see cref="ReadResult"/> types.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Throws <see cref="OperationCanceledException"/> if I/O operation is canceled.
    /// </summary>
    /// <param name="result">The result of I/O operation to check.</param>
    /// <param name="token">The token that may be a source of cancellation.</param>
    /// <exception cref="OperationCanceledException">I/O operation has been canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfCancellationRequested(this in FlushResult result, CancellationToken token = default)
    {
        if (result.IsCanceled)
            throw new OperationCanceledException(token.IsCancellationRequested ? token : new CancellationToken(true));
    }

    internal static void ThrowIfCancellationRequested(this in ReadResult result, PipeReader reader, CancellationToken token)
    {
        if (result.IsCanceled)
        {
            reader.AdvanceTo(result.Buffer.Start);
            throw new OperationCanceledException(token.IsCancellationRequested ? token : new CancellationToken(true));
        }
    }
}
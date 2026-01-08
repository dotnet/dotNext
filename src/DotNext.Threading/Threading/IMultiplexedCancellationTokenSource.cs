namespace DotNext.Threading;

/// <summary>
/// Represents multiplexed cancellation token source.
/// </summary>
public interface IMultiplexedCancellationTokenSource
{
    /// <summary>
    /// Gets the multiplexed token.
    /// </summary>
    CancellationToken Token { get; }
    
    /// <summary>
    /// Gets the cancellation origin.
    /// </summary>
    CancellationToken CancellationOrigin { get; }
}

internal interface IMultiplexedCancellationTokenSourceWithTimeout : IMultiplexedCancellationTokenSource
{
    bool IsTimedOut { get; }
}

/// <summary>
/// Provides extension methods for <see cref="IMultiplexedCancellationTokenSource"/>
/// </summary>
public static class MultiplexedCancellationTokenSource
{
    /// <summary>
    /// Determines whether the operation was canceled by the specified source.
    /// </summary>
    /// <param name="source">The linked token source.</param>
    /// <param name="e">The exception to analyze.</param>
    /// <param name="token">The token to check</param>
    /// <returns><see langword="true"/> indicates that the cancellation caused by <paramref name="source"/> and <see cref="IMultiplexedCancellationTokenSource.CancellationOrigin"/> is <paramref name="token"/>.</returns>
    public static bool CausedBy<TSource>(this OperationCanceledException e, TSource source, CancellationToken token)
        where TSource : struct, IMultiplexedCancellationTokenSource
        => e.CancellationToken == source.Token && source.CancellationOrigin == token;

    private static bool CausedByTimeout<TSource>(OperationCanceledException e, TSource scope)
        where TSource : struct, IMultiplexedCancellationTokenSourceWithTimeout
        => e.CancellationToken == scope.Token && scope.IsTimedOut;

    /// <summary>
    /// Determines whether the operation was canceled by the specified source due to timeout.
    /// </summary>
    /// <param name="scope">The linked token source.</param>
    /// <param name="e">The exception to analyze.</param>
    /// <returns><see langword="true"/> indicates that the <paramref name="e"/> is triggered by the timeout
    /// associated with <paramref name="scope"/>; otherwise, <see langword="false"/>.</returns>
    public static bool CausedByTimeout(this OperationCanceledException e, CancellationTokenMultiplexer.Scope scope)
        => CausedByTimeout<CancellationTokenMultiplexer.Scope>(e, scope);

    /// <summary>
    /// Determines whether the operation was canceled by the specified source due to timeout.
    /// </summary>
    /// <param name="scope">The linked token source.</param>
    /// <param name="e">The exception to analyze.</param>
    /// <returns><see langword="true"/> indicates that the <paramref name="e"/> is triggered by the timeout
    /// associated with <paramref name="scope"/>; otherwise, <see langword="false"/>.</returns>
    public static bool CausedByTimeout(this OperationCanceledException e, CancellationTokenMultiplexer.ScopeWithTimeout scope)
        => CausedByTimeout<CancellationTokenMultiplexer.ScopeWithTimeout>(e, scope);
}
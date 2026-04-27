namespace DotNext.Threading;

/// <summary>
/// Represents multiplexed cancellation token source.
/// </summary>
public interface IMultiplexedCancellationTokenSource : IDisposable
{
    /// <summary>
    /// Gets the multiplexed token.
    /// </summary>
    CancellationToken Token { get; }
    
    /// <summary>
    /// Gets the cancellation origin.
    /// </summary>
    CancellationToken CancellationOrigin { get; }

    /// <summary>
    /// Creates a token source for the specified cancellation token state.
    /// </summary>
    /// <param name="canceled">The canceled state for the token.</param>
    /// <returns>The token source that represents the specific cancellation token state.</returns>
    public static IMultiplexedCancellationTokenSource Create(bool canceled)
        => canceled ? CancellationTokenWrapper.Canceled : CancellationTokenWrapper.NonCanceled;

    internal static IMultiplexedCancellationTokenSource Create(CancellationToken token)
    {
        CancellationTokenWrapper result;
        if (CancellationTokenWrapper.Canceled.Token == token)
        {
            result = CancellationTokenWrapper.Canceled;
        }
        else if (CancellationTokenWrapper.NonCanceled.Token == token)
        {
            result = CancellationTokenWrapper.NonCanceled;
        }
        else
        {
            result = new(token);
        }

        return result;
    }
}

file sealed class CancellationTokenWrapper(CancellationToken token) : IMultiplexedCancellationTokenSource
{
    public static readonly CancellationTokenWrapper Canceled = new(canceled: true);
    public static readonly CancellationTokenWrapper NonCanceled = new(canceled: false);

    private CancellationTokenWrapper(bool canceled)
        : this(new CancellationToken(canceled))
    {
    }
    
    void IDisposable.Dispose()
    {
        // nothing to do
    }

    public CancellationToken Token => token;

    CancellationToken IMultiplexedCancellationTokenSource.CancellationOrigin => token;
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
        where TSource : IMultiplexedCancellationTokenSource
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

    /// <summary>
    /// Extends <see cref="CancellationToken"/> type.
    /// </summary>
    extension(CancellationToken)
    {
        /// <summary>
        /// Combines multiple cancellation tokens.
        /// </summary>
        /// <param name="tokens">The tokens that can be combined.</param>
        /// <returns>The multiplexed cancellation token source.</returns>
        public static IMultiplexedCancellationTokenSource Combine(params ReadOnlySpan<CancellationToken> tokens)
            => CancellationTokenMultiplexer.Create(tokens);
    }
}
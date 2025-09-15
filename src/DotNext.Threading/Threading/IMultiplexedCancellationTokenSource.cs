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
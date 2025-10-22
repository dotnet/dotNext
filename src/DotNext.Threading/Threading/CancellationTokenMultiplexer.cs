using System.Diagnostics;

namespace DotNext.Threading;

/// <summary>
/// Represents cancellation token multiplexer.
/// </summary>
/// <remarks>
/// The multiplexer provides a pool of <see cref="CancellationTokenSource"/> to combine
/// the cancellation tokens.
/// </remarks>
public sealed partial class CancellationTokenMultiplexer
{
    private readonly int maximumRetained = int.MaxValue;
    private volatile int count;
    private volatile PooledCancellationTokenSource? firstInPool;

    /// <summary>
    /// Gets or sets the maximum retained <see cref="CancellationTokenSource"/> instances.
    /// </summary>
    public int MaximumRetained
    {
        get => maximumRetained;
        init => maximumRetained = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Combines the multiple tokens.
    /// </summary>
    /// <param name="tokens">The tokens to be combined.</param>
    /// <returns>The scope that contains a single multiplexed token.</returns>
    public Scope Combine(ReadOnlySpan<CancellationToken> tokens) // TODO: use params
    {
        Scope scope;
        switch (tokens)
        {
            case []:
                scope = new();
                break;
            case [var token]:
                scope = new(token);
                break;
            case [var token1, var token2]:
                if (!token1.CanBeCanceled || token1 == token2)
                {
                    scope = new(token2);
                }
                else if (!token2.CanBeCanceled)
                {
                    scope = new(token1);
                }
                else
                {
                    goto default;
                }

                break;
            default:
                scope = new(this, tokens);
                break;
        }

        return scope;
    }

    /// <summary>
    /// Combines the multiple tokens and the timeout.
    /// </summary>
    /// <remarks>
    /// The cancellation triggered by the timeout can be detected by checking <see cref="Scope.IsTimedOut"/>.
    /// </remarks>
    /// <param name="timeout">The timeout that could trigger the cancellation.</param>
    /// <param name="tokens">The tokens to be combined.</param>
    /// <returns>The scope that represents the multiplexed token.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative or too large.</exception>
    public Scope Combine(TimeSpan timeout, ReadOnlySpan<CancellationToken> tokens) => timeout.Ticks switch
    {
        0L => new(TimedOutToken),
        Timeout.InfiniteTicks => Combine(tokens),
        < 0L or > Timeout.MaxTimeoutParameterTicks => throw new ArgumentOutOfRangeException(nameof(timeout)),
        _ => new(this, timeout, tokens)
    };

    /// <summary>
    /// Combines the multiple tokens and sets the timeout later.
    /// </summary>
    /// <param name="tokens">The tokens to be combined.</param>
    /// <returns>The scope that represents the multiplexed token.</returns>
    public ScopeWithTimeout CombineAndSetTimeoutLater(ReadOnlySpan<CancellationToken> tokens) // TODO: use params
        => new(this, tokens);

    private void Return(PooledCancellationTokenSource source)
    {
        // try to increment the counter
        for (int current = count, tmp; current < maximumRetained; current = tmp)
        {
            tmp = Interlocked.CompareExchange(ref count, current + 1, current);
            if (tmp == current)
            {
                ReturnCore(source);
                break;
            }
        }
    }

    private void ReturnCore(PooledCancellationTokenSource source)
    {
        for (PooledCancellationTokenSource? current = firstInPool, tmp;; current = tmp)
        {
            source.Next = current;
            tmp = Interlocked.CompareExchange(ref firstInPool, source, current);

            if (ReferenceEquals(tmp, source))
                break;
        }
    }

    private PooledCancellationTokenSource Rent()
    {
        var current = firstInPool;
        for (PooledCancellationTokenSource? tmp;; current = tmp)
        {
            if (current is null)
            {
                current = new();
                break;
            }

            tmp = Interlocked.CompareExchange(ref firstInPool, current.Next, current);
            if (!ReferenceEquals(tmp, current))
                continue;

            current.Next = null;
            var actualCount = Interlocked.Decrement(ref count);
            Debug.Assert(actualCount >= 0L);
            break;
        }

        return current;
    }

    private PooledCancellationTokenSource Rent(ReadOnlySpan<CancellationToken> tokens)
    {
        var source = Rent();
        Debug.Assert(source.Count is 0);

        source.AddRange(tokens);
        Debug.Assert(source.Count == tokens.Length);
        
        return source;
    }
}
using System.Collections.Concurrent;
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
    private readonly ConcurrentQueue<PooledCancellationTokenSource> sources = new();
    private readonly int maximumRetained = int.MaxValue;
    private volatile int count;
    private PooledCancellationTokenSource? fastSource;

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
    public Scope Combine(params ReadOnlySpan<CancellationToken> tokens)
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
    public Scope Combine(TimeSpan timeout, params ReadOnlySpan<CancellationToken> tokens) => timeout.Ticks switch
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
    public ScopeWithTimeout CombineAndSetTimeoutLater(params ReadOnlySpan<CancellationToken> tokens)
        => new(this, tokens);

    private void Return(PooledCancellationTokenSource src)
    {
        if (fastSource is null && Interlocked.CompareExchange(ref fastSource, src, null) is null)
        {
            // nothing to do
        }
        else if (Interlocked.Increment(ref count) <= maximumRetained)
        {
            sources.Enqueue(src);
        }
        else
        {
            Interlocked.Decrement(ref count); // rollback the increment
        }
    }

    private PooledCancellationTokenSource Rent()
    {
        if (fastSource is { } src && Interlocked.CompareExchange(ref fastSource, null, src) == src)
        {
            // nothing to do
        }
        else if (sources.TryDequeue(out src))
        {
            Interlocked.Decrement(ref count);
        }
        else
        {
            src = new();
        }

        return src;
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
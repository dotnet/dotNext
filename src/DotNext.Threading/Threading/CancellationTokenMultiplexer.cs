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

        foreach (var token in tokens)
        {
            source.Add(token);
        }

        return source;
    }
}
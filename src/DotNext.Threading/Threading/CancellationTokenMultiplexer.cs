using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

/// <summary>
/// Represents cancellation token multiplexer.
/// </summary>
/// <remarks>
/// The multiplexer provides a pool of <see cref="CancellationTokenSource"/> to combine
/// the cancellation tokens.
/// </remarks>
public sealed class CancellationTokenMultiplexer
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
    public Scope Combine(ReadOnlySpan<CancellationToken> tokens)
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

    /// <summary>
    /// Represents a 
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Scope : IMultiplexedCancellationTokenSource, IDisposable, IAsyncDisposable
    {
        private readonly CancellationTokenMultiplexer multiplexer;
        private readonly PooledCancellationTokenSource source;

        internal Scope(CancellationTokenMultiplexer multiplexer, ReadOnlySpan<CancellationToken> tokens)
        {
            this.multiplexer = multiplexer;
            source = multiplexer.Rent();

            foreach (var token in tokens)
            {
                source.Add(token);
            }
        }

        /// <summary>
        /// Gets the cancellation token.
        /// </summary>
        public CancellationToken Token => source.Token;

        /// <summary>
        /// Gets the cancellation origin if <see cref="Token"/> is in canceled state.
        /// </summary>
        public CancellationToken CancellationOrigin => source.CancellationOrigin;

        /// <inheritdoc/>
        public void Dispose()
        {
            for (var i = 0; i < source.Count; i++)
            {
                source[i].Dispose();
            }

            // now we sure that no one can cancel the source concurrently
            Return(multiplexer, source);
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync() => ReturnAsync(multiplexer, source);

        private static async ValueTask ReturnAsync(CancellationTokenMultiplexer multiplexer, PooledCancellationTokenSource source)
        {
            for (var i = 0; i < source.Count; i++)
            {
                await source[i].DisposeAsync().ConfigureAwait(false);
            }

            Return(multiplexer, source);
        }

        private static void Return(CancellationTokenMultiplexer multiplexer, PooledCancellationTokenSource source)
        {
            source.Clear();
            if (source.IsCancellationRequested)
            {
                source.Dispose();
            }
            else
            {
                multiplexer.Return(source);
            }
        }
    }

    private sealed class PooledCancellationTokenSource : LinkedCancellationTokenSource
    {
        private const int Capacity = 3;
        private (CancellationTokenRegistration, CancellationTokenRegistration, CancellationTokenRegistration) inlineList;
        private List<CancellationTokenRegistration>? extraTokens;
        private int tokenCount;
        internal PooledCancellationTokenSource? Next;

        public void Add(CancellationToken token)
            => Add() = Attach(token);

        private ref CancellationTokenRegistration Add()
        {
            Span<CancellationTokenRegistration> registrations;
            var index = tokenCount;
            if (++tokenCount < Capacity)
            {
                registrations = inlineList.AsSpan();
            }
            else
            {
                extraTokens ??= new();
                extraTokens.Add(default);
                registrations = CollectionsMarshal.AsSpan(extraTokens);
                index -= Capacity;
            }

            return ref registrations[index];
        }

        public int Count => tokenCount;

        public ref CancellationTokenRegistration this[int index]
        {
            get
            {
                Span<CancellationTokenRegistration> registrations;
                if (index < Capacity)
                {
                    registrations = inlineList.AsSpan();
                }
                else
                {
                    registrations = CollectionsMarshal.AsSpan(extraTokens);
                    index -= Capacity;
                }

                return ref registrations[index];
            }
        }

        public void Clear()
        {
            inlineList = default;
            extraTokens?.Clear();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                extraTokens = null; // help GC
            }

            base.Dispose(disposing);
        }
    }
}
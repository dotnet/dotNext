using System.Runtime.InteropServices;

namespace DotNext.Threading;

partial class CancellationTokenMultiplexer
{
    /// <summary>
    /// Represents a scope that controls the lifetime of the multiplexed cancellation token.
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
        /// Gets the cancellation token that can be canceled by any of the multiplexed tokens.
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
}
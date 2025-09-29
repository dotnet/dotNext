using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Runtime;

partial class CancellationTokenMultiplexer
{
    /// <summary>
    /// Represents a scope that controls the lifetime of the multiplexed cancellation token.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Scope : IMultiplexedCancellationTokenSource, IDisposable, IAsyncDisposable
    {
        // CancellationToken is just a wrapper over CancellationTokenSource.
        // For optimization purposes, if only one token is passed to the scope, we can inline the underlying CTS
        // to this structure.
        private readonly ValueTuple<object> multiplexerOrToken;
        private readonly PooledCancellationTokenSource? source;

        internal Scope(CancellationTokenMultiplexer multiplexer, ReadOnlySpan<CancellationToken> tokens)
        {
            multiplexerOrToken = new(multiplexer);
            source = multiplexer.Rent(tokens);
        }

        internal Scope(CancellationToken token)
            => multiplexerOrToken = InlineToken(token);

        private static ValueTuple<object> InlineToken(CancellationToken token)
            => CanInlineToken ? Unsafe.BitCast<CancellationToken, ValueTuple<object>>(token) : new(token);

        private static CancellationToken GetToken(ValueTuple<object> value)
            => CanInlineToken ? Unsafe.BitCast<ValueTuple<object>, CancellationToken>(value) : (CancellationToken)value.Item1;

        // This property checks whether the reinterpret cast CancellationToken => CancellationTokenSource
        // is safe. If not, just box the token.
        private static bool CanInlineToken => Intrinsics.AreCompatible<CancellationToken, ValueTuple<object>>()
                                              && RuntimeHelpers.IsReferenceOrContainsReferences<CancellationToken>();

        /// <summary>
        /// Gets the cancellation token that can be canceled by any of the multiplexed tokens.
        /// </summary>
        public CancellationToken Token => source?.Token ?? GetToken(multiplexerOrToken);

        /// <summary>
        /// Gets the cancellation origin if <see cref="Token"/> is in canceled state.
        /// </summary>
        public CancellationToken CancellationOrigin => source?.CancellationOrigin ?? GetToken(multiplexerOrToken);

        /// <inheritdoc/>
        public void Dispose()
        {
            if (source is not null)
            {
                Debug.Assert(multiplexerOrToken.Item1 is CancellationTokenMultiplexer);

                for (var i = 0; i < source.Count; i++)
                {
                    source[i].Dispose();
                }

                // now we sure that no one can cancel the source concurrently
                Return(Unsafe.As<CancellationTokenMultiplexer>(multiplexerOrToken.Item1), source);
            }
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
            => source is not null
                ? ReturnAsync(Unsafe.As<CancellationTokenMultiplexer>(multiplexerOrToken.Item1), source)
                : ValueTask.CompletedTask;

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
            source.Reset();
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
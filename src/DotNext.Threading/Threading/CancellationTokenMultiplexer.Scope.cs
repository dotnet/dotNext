using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using static Timeout;

partial class CancellationTokenMultiplexer
{
    private interface IMultiplexedTokenScope : IMultiplexedCancellationTokenSource, IDisposable, IAsyncDisposable
    {
        bool IsTimedOut { get; }
    }
    
    /// <summary>
    /// Represents a scope that controls the lifetime of the multiplexed cancellation token.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Scope : IMultiplexedTokenScope
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

        internal Scope(CancellationTokenMultiplexer multiplexer, TimeSpan timeout, ReadOnlySpan<CancellationToken> tokens)
        {
            multiplexerOrToken = new(multiplexer);
            source = multiplexer.Rent(tokens);
            source.RegisterTimeoutHandler();
            source.CancelAfter(timeout);
        }

        internal Scope(CancellationToken token)
            => multiplexerOrToken = InlineToken(token);

        private static ValueTuple<object> InlineToken(CancellationToken token)
            => LinkedCancellationTokenSource.CanInlineToken
                ? Unsafe.BitCast<CancellationToken, ValueTuple<object>>(token)
                : new(token);

        private static CancellationToken GetToken(ValueTuple<object> value)
            => LinkedCancellationTokenSource.CanInlineToken
                ? Unsafe.BitCast<ValueTuple<object>, CancellationToken>(value)
                : (CancellationToken)value.Item1;

        /// <inheritdoc cref="IMultiplexedCancellationTokenSource.Token"/>
        public CancellationToken Token => source?.Token ?? GetToken(multiplexerOrToken);

        /// <inheritdoc cref="IMultiplexedCancellationTokenSource.CancellationOrigin"/>
        public CancellationToken CancellationOrigin => source?.CancellationOrigin ?? GetToken(multiplexerOrToken);

        /// <summary>
        /// Gets a value indicating that the multiplexed token is cancelled by the timeout.
        /// </summary>
        public bool IsTimedOut => source?.IsRootCause ?? false;

        internal void SetTimeout(TimeSpan value)
        {
            if (source is not null)
            {
                source.RegisterTimeoutHandler();
                source.CancelAfter(value);
            }
        }

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
            if (source.TryReset())
            {
                multiplexer.Return(source);
            }
            else
            {
                source.Dispose();
            }
        }
    }
    
    /// <summary>
    /// Represents a scope that controls the lifetime of the multiplexed cancellation token and allows to specify the timeout.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ScopeWithTimeout : IMultiplexedTokenScope
    {
        private readonly Scope scope;

        internal ScopeWithTimeout(CancellationTokenMultiplexer multiplexer, ReadOnlySpan<CancellationToken> tokens)
            => scope = new(multiplexer, tokens);

        /// <summary>
        /// Sets the optional timeout.
        /// </summary>
        /// <seealso cref="CancellationTokenMultiplexer.CombineAndSetTimeoutLater"/>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public TimeSpan Timeout
        {
            set
            {
                Validate(value);

                scope.SetTimeout(value);
            }
        }

        /// <inheritdoc cref="IMultiplexedCancellationTokenSource.Token"/>
        public CancellationToken Token => scope.Token;

        /// <inheritdoc cref="IMultiplexedCancellationTokenSource.CancellationOrigin"/>
        public CancellationToken CancellationOrigin => scope.CancellationOrigin;

        /// <inheritdoc cref="Scope.IsTimedOut"/>
        public bool IsTimedOut => scope.IsTimedOut;

        /// <inheritdoc/>
        public void Dispose() => scope.Dispose();

        /// <inheritdoc/>
        public ValueTask DisposeAsync() => scope.DisposeAsync();
    }
}
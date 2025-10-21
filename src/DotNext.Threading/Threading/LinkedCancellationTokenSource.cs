using System.Diagnostics;
using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Threading;

using Runtime;

/// <summary>
/// Gets cancellation token source that allows to obtain the token that causes
/// cancellation.
/// </summary>
/// <remarks>
/// This source is not resettable. Calling of <see cref="CancellationTokenSource.TryReset"/>
/// may lead to unpredictable results.
/// </remarks>
public abstract class LinkedCancellationTokenSource : CancellationTokenSource, IMultiplexedCancellationTokenSource
{
    // represents inlined CancellationToken
    private ValueTuple<object?> cancellationOrigin;

    private protected LinkedCancellationTokenSource() => CancellationOrigin = Token;
    
    private protected CancellationTokenRegistration Attach(CancellationToken token)
    {
        return token.UnsafeRegister(OnCanceled, this);
        
        static void OnCanceled(object? source, CancellationToken token)
        {
            Debug.Assert(source is LinkedCancellationTokenSource);

            Unsafe.As<LinkedCancellationTokenSource>(source).Cancel(token);
        }
    }

    private static ValueTuple<object?> InlineToken(CancellationToken token) => CanInlineToken
        ? Unsafe.BitCast<CancellationToken, ValueTuple<object?>>(token)
        : new(token);
    
    internal void AttachTimeoutHandler()
    {
        Token.UnsafeRegister(OnTimeout, this);

        static void OnTimeout(object? source, CancellationToken token)
        {
            Debug.Assert(source is LinkedCancellationTokenSource);

            Unsafe.As<LinkedCancellationTokenSource>(source).TrySetCancellationOrigin(token);
        }
    }

    private void Cancel(CancellationToken token)
    {
        if (TrySetCancellationOrigin(token))
        {
            try
            {
                Cancel(throwOnFirstException: false);
            }
            catch (ObjectDisposedException)
            {
                // suppress exception
            }
        }
    }
    
    private bool TrySetCancellationOrigin(CancellationToken token)
    {
        var inlinedToken = InlineToken(token);
        return Interlocked.CompareExchange(ref cancellationOrigin.Item1, inlinedToken.Item1, comparand: null) is null;
    }

    /// <summary>
    /// Gets the token caused cancellation.
    /// </summary>
    /// <remarks>
    /// It is recommended to request this property after cancellation.
    /// </remarks>
    public CancellationToken CancellationOrigin
    {
        get => CanInlineToken
            ? Unsafe.BitCast<ValueTuple<object?>, CancellationToken>(cancellationOrigin)
            : cancellationOrigin.Item1 is null
                ? CancellationToken.None
                : Unsafe.Unbox<CancellationToken>(cancellationOrigin.Item1);

        private protected set => cancellationOrigin = InlineToken(value);
    }

    /// <summary>
    /// Gets a value indicating that this token source is cancelled by the timeout associated with this source,
    /// or by calling <see cref="CancellationTokenSource.Cancel()"/> manually.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal bool IsRootCause => CanInlineToken ? ReferenceEquals(this, cancellationOrigin.Item1) : CancellationOrigin == Token;
    
    // This property checks whether the reinterpret cast CancellationToken => CancellationTokenSource
    // is safe. If not, just box the token.
    internal static bool CanInlineToken => Intrinsics.AreCompatible<CancellationToken, ValueTuple<object>>()
                                           && RuntimeHelpers.IsReferenceOrContainsReferences<CancellationToken>();
}
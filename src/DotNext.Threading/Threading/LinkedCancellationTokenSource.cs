using System.Diagnostics;
using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Threading;

using Runtime;
using InlinedToken = ValueTuple<object?>;

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
    private InlinedToken cancellationOrigin;

    private protected LinkedCancellationTokenSource()
    {
    }
    
    private protected CancellationTokenRegistration Attach(CancellationToken token)
    {
        return token.UnsafeRegister(OnCanceled, this);
        
        static void OnCanceled(object? source, CancellationToken token)
        {
            Debug.Assert(source is LinkedCancellationTokenSource);

            Unsafe.As<LinkedCancellationTokenSource>(source).Cancel(token);
        }
    }

    private static InlinedToken InlineToken(CancellationToken token) => CanInlineToken
        ? Unsafe.BitCast<CancellationToken, InlinedToken>(token)
        : new(token);
    
    internal void RegisterTimeoutHandler()
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

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private object RawToken
    {
        get
        {
            // There is rare race condition that could lead to incorrect detection of the cancellation root:
            // the linked token gets canceled in the same time as the timeout happens for this source.
            // In this case, the timeout thread resumes the attached callbacks. However, the order
            // of these callbacks is not guaranteed. Thus, OnTimeout is not yet called, and cancellationOrigin
            // is still null. The resumed thread observes CancellationOrigin == Token. But then,
            // the linked token calls Cancel callback that sets cancellationOrigin to the real token.
            // In that case, CancellationOrigin != Token. It means that the consumer of CancellationOrigin
            // can see two different values when the property getter is called sequentially. This
            // is non-deterministic behavior. Currently, nothing we can do with incorrect detection
            // of the cancellation root due to absence of the necessary methods in CTS. But we can
            // achieve deterministic behavior for CancellationOrigin and IsRootCause properties:
            // if cancellation is requested for this source by timeout, switch cancellationOrigin to not-null
            // value in getter to prevent concurrent overwrite by the linked token cancellation callback.
            var tokenCopy = cancellationOrigin.Item1;
            if (CanInlineToken)
            {
                tokenCopy ??= IsCancellationRequested
                    ? Interlocked.CompareExchange(ref cancellationOrigin.Item1, this, comparand: null) ?? this
                    : this;
            }
            else if (tokenCopy is null)
            {
                object boxedToken = Token;
                tokenCopy = Interlocked.CompareExchange(ref cancellationOrigin.Item1, boxedToken, comparand: null) ?? boxedToken;
            }

            return tokenCopy;
        }
    }

    /// <summary>
    /// Gets the token caused cancellation.
    /// </summary>
    /// <remarks>
    /// It is recommended to request this property after cancellation.
    /// </remarks>
    public CancellationToken CancellationOrigin
    {
        get
        {
            var rawToken = RawToken;
            return CanInlineToken
                ? Unsafe.BitCast<InlinedToken, CancellationToken>(new(rawToken))
                : Unsafe.Unbox<CancellationToken>(rawToken);
        }

        private protected set
        {
            var tokenCopy = InlineToken(value);
            Volatile.Write(ref cancellationOrigin.Item1, tokenCopy.Item1);
        }
    }

    /// <summary>
    /// Gets a value indicating that this token source is cancelled by the timeout associated with this source,
    /// or by calling <see cref="CancellationTokenSource.Cancel()"/> manually.
    /// </summary>
    internal bool IsRootCause
    {
        get
        {
            var rawToken = RawToken;
            return CanInlineToken
                ? ReferenceEquals(rawToken, this)
                : Unsafe.Unbox<CancellationToken>(rawToken) == Token;
        }
    }

    // This property checks whether the reinterpret cast CancellationToken => CancellationTokenSource
    // is safe. If not, just box the token.
    internal static bool CanInlineToken => Intrinsics.AreCompatible<CancellationToken, InlinedToken>()
                                           && RuntimeHelpers.IsReferenceOrContainsReferences<CancellationToken>();

    internal static LinkedCancellationTokenSource? Combine(ref CancellationToken first, CancellationToken second)
    {
        var result = default(LinkedCancellationTokenSource);

        if (first == second)
        {
            // nothing to do, just return from this method
        }
        else if (!first.CanBeCanceled || second.IsCancellationRequested)
        {
            first = second;
        }
        else if (second.CanBeCanceled && !first.IsCancellationRequested)
        {
            result = new Linked2CancellationTokenSource(in first, in second);
            first = result.Token;
        }

        return result;
    }
}

file sealed class Linked2CancellationTokenSource : LinkedCancellationTokenSource
{
    private readonly CancellationTokenRegistration registration1, registration2;

    internal Linked2CancellationTokenSource(in CancellationToken token1, in CancellationToken token2)
    {
        Debug.Assert(token1.CanBeCanceled);
        Debug.Assert(token2.CanBeCanceled);

        registration1 = Attach(token1);
        registration2 = Attach(token2);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            registration1.Unregister();
            registration2.Unregister();
        }

        base.Dispose(disposing);
    }
}
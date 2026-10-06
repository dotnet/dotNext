using System.Diagnostics;
using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Threading;

using Runtime.CompilerServices;
using InlinedToken = ValueTuple<object?>;

internal abstract class LinkedCancellationTokenSource : CancellationTokenSource, IMultiplexedCancellationTokenSource
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

            Unsafe.As<LinkedCancellationTokenSource>(source).NotifyCancellation(token);
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

    private void NotifyCancellation(CancellationToken token)
    {
        if (TrySetCancellationOrigin(token))
        {
            OnCanceled();
        }
    }

    private protected virtual void OnCanceled()
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

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private protected bool IsCancellationOriginSet => Volatile.Read(in cancellationOrigin.Item1) is not null;

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
    /// Gets a value indicating that this token source is canceled by the timeout associated with this source,
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
    internal static bool CanInlineToken => Unsafe.AreCompatible<CancellationToken, InlinedToken>()
                                           && RuntimeHelpers.IsReferenceOrContainsReferences<CancellationToken>();
}
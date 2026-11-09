using System.Diagnostics;
using Debug = System.Diagnostics.Debug;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Threading;

using InlinedToken = ValueTuple<object?>;

internal abstract class LinkedCancellationTokenSource : CancellationTokenSource, IMultiplexedCancellationTokenSource
{
    // represents inlined CancellationToken
    private InlinedToken cancellationOrigin;
    
    private protected CancellationTokenRegistration Attach(CancellationToken token)
    {
        return token.UnsafeRegister(OnCanceled, this);
        
        static void OnCanceled(object? source, CancellationToken token)
        {
            Debug.Assert(source is LinkedCancellationTokenSource);

            Unsafe.As<LinkedCancellationTokenSource>(source).NotifyCancellation(token);
        }
    }

    private static InlinedToken InlineToken(CancellationToken token) => CancellationToken.CanBeInlined
        ? Unsafe.BitCast<CancellationToken, InlinedToken>(token)
        : new(token);
    
    private void NotifyCancellation(CancellationToken token)
    {
        // If this source is already canceled (by timeout), the linked token cannot be the origin
        if (!IsCancellationRequested && TrySetCancellationOrigin(token))
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

    // null means that the source is not canceled yet
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private object? RawToken
    {
        get
        {
            var tokenCopy = Volatile.Read(in cancellationOrigin.Item1);
            if (tokenCopy is null && IsCancellationRequested)
            {
                // Canceled by timeout. Claim the origin to make the result stable for the subsequent calls
                var self = CancellationToken.CanBeInlined ? this : (object)Token;
                tokenCopy = Interlocked.CompareExchange(ref cancellationOrigin.Item1, self, comparand: null) ?? self;
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
            return CancellationToken.CanBeInlined
                ? Unsafe.BitCast<InlinedToken, CancellationToken>(new(rawToken ?? this))
                : rawToken is null ? Token : Unsafe.Unbox<CancellationToken>(rawToken);
        }
    }

    private protected void ResetCancellationOrigin() => Volatile.Write(ref cancellationOrigin.Item1, null);

    /// <summary>
    /// Gets a value indicating that this token source is canceled by the timeout associated with this source,
    /// or by calling <see cref="CancellationTokenSource.Cancel()"/> manually.
    /// </summary>
    internal bool IsRootCause
    {
        get
        {
            var rawToken = RawToken;
            return CancellationToken.CanBeInlined
                ? ReferenceEquals(rawToken, this)
                : rawToken is not null && Unsafe.Unbox<CancellationToken>(rawToken) == Token;
        }
    }
}
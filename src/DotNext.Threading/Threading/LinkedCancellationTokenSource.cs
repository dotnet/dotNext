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

    /// <summary>
    /// Gets the token caused cancellation.
    /// </summary>
    /// <remarks>
    /// It is recommended to request this property after cancellation.
    /// </remarks>
    public CancellationToken CancellationOrigin
    {
        get => new InlinedToken(Volatile.Read(in cancellationOrigin.Item1)) is { Item1: not null } tokenCopy
            ? CanInlineToken
                ? Unsafe.BitCast<InlinedToken, CancellationToken>(tokenCopy)
                : Unsafe.Unbox<CancellationToken>(tokenCopy.Item1)
            : Token;

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
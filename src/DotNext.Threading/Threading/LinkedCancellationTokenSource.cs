using Debug = System.Diagnostics.Debug;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Threading;

/// <summary>
/// Gets cancellation token source that allows to obtain the token that causes
/// cancellation.
/// </summary>
/// <remarks>
/// This source is not resettable. Calling of <see cref="CancellationTokenSource.TryReset"/>
/// may lead to unpredictable results.
/// </remarks>
public abstract class LinkedCancellationTokenSource : CancellationTokenSource
{
    private protected static readonly Action<object?, CancellationToken> CancellationCallback;

    static LinkedCancellationTokenSource()
    {
        CancellationCallback = OnCanceled;

        static void OnCanceled(object? source, CancellationToken token)
        {
            Debug.Assert(source is LinkedCancellationTokenSource);

            Unsafe.As<LinkedCancellationTokenSource>(source).Cancel(token);
        }
    }

    private const int UnsetStatus = 0;
    private const int InProgressStatus = 1;
    private const int ReadyStatus = 2;
    private volatile int status;
    private CancellationToken originalToken;

    private protected LinkedCancellationTokenSource()
    {
    }

    private void Cancel(CancellationToken token)
    {
        if (Interlocked.CompareExchange(ref status, InProgressStatus, UnsetStatus) is UnsetStatus)
        {
            try
            {
                originalToken = token;
                Cancel(throwOnFirstException: false);
            }
            catch (ObjectDisposedException)
            {
                // suppress exception
            }
            finally
            {
                status = ReadyStatus;
            }
        }
    }

    /// <summary>
    /// Gets the token caused cancellation.
    /// </summary>
    /// <remarks>
    /// It is recommended to request this property after cancellation.
    /// </remarks>
    public CancellationToken CancellationOrigin
        => status is ReadyStatus ? originalToken : Token;

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            originalToken = default;
        }

        status = UnsetStatus;
        base.Dispose(disposing);
    }
}

internal sealed class Linked2CancellationTokenSource : LinkedCancellationTokenSource
{
    private readonly CancellationTokenRegistration registration1, registration2;

    internal Linked2CancellationTokenSource(in CancellationToken token1, in CancellationToken token2)
    {
        Debug.Assert(token1.CanBeCanceled);
        Debug.Assert(token2.CanBeCanceled);

        registration1 = token1.UnsafeRegister(CancellationCallback, this);
        registration2 = token2.UnsafeRegister(CancellationCallback, this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            registration1.Dispose();
            registration2.Dispose();
        }

        base.Dispose(disposing);
    }
}
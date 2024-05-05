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

    private Atomic.Boolean status;

    private protected LinkedCancellationTokenSource() => CancellationOrigin = Token;

    private void Cancel(CancellationToken token)
    {
        if (status.FalseToTrue())
        {
            CancellationOrigin = token;
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

    /// <summary>
    /// Gets the token caused cancellation.
    /// </summary>
    /// <remarks>
    /// It is recommended to request this property after cancellation.
    /// </remarks>
    public CancellationToken CancellationOrigin
    {
        get;
        private set;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}
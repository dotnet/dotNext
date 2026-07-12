using System.Runtime.InteropServices;

namespace DotNext.Threading.Tasks;

partial class ManualResetCompletionSource
{
    private void CancellationRequested<TReason>(TReason reason)
        where TReason : struct, ICancellationReason, allows ref struct
    {
        if (BeginCompletion(reason.Version))
        {
            try
            {
                if (reason.TryGetToken(out var token))
                {
                    CompleteAsCanceled(token);

                    // The executing callback cannot be unregistered (its node is already claimed by the CTS),
                    // so drop the registration to let Reset() reuse the cached version box and the timer.
                    // This also covers mass cancellation: when a single token cancels many sources, Reset()
                    // of an already-notified source can run while the token's callback list is still draining,
                    // so UnregisterAndReuse() would otherwise see the cancellation as in-flight and refuse reuse.
                    // This write is safe against Reset(): we're inside the Completing window, so ResetCore spins.
                    // It can race the field store in EnableCancellation if cancellation lands during activation;
                    // both torn outcomes are benign: (id, 0) reads as a default token, (0, node) fails Unregister
                    // with id == 0, and either way UnregisterAndReuse degrades to a conservative non-reuse.
                    tokenTracker = default;
                }
                else
                {
                    CompleteAsTimedOut();

                    // Tells TryReset() that the fired timer callback is our own completed one, so the timer
                    // and the version box can be reused even while the callback is still unwinding.
                    // Set after CompleteAsTimedOut(): if the user-defined timeout exception factory throws,
                    // the flag stays false and the timer is conservatively disposed at reset time.
                    // The write is safe against Reset(): we're inside the Completing window, so ResetCore spins.
                    completedByTimeout = true;
                }
            }
            finally
            {
                if (EndCompletion())
                {
                    NotifyConsumer();
                }
            }
        }
    }
    
    private interface ICancellationReason
    {
        bool TryGetToken(out CancellationToken token);
        
        short Version { get; }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct CancellationReason(short version, CancellationToken cancellationToken) : ICancellationReason
    {
        bool ICancellationReason.TryGetToken(out CancellationToken token)
        {
            token = cancellationToken;
            return true;
        }

        short ICancellationReason.Version => version;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct TimeoutReason(short version) : ICancellationReason
    {
        bool ICancellationReason.TryGetToken(out CancellationToken token)
        {
            token = CancellationToken.None;
            return false;
        }
        
        short ICancellationReason.Version => version;
    }
}
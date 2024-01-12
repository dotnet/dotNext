using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft;

internal partial class LeaderState<TMember>
{
    private sealed class Lease : CancellationTokenSource
    {
        internal readonly new CancellationToken Token; // cached to avoid ObjectDisposedException

        internal Lease() => Token = base.Token;

        internal bool TryRenew(TimeSpan leaseTime)
        {
            try
            {
                // refreshes the internal timer without invalidating cancellation subscriptions
                if (leaseTime <= TimeSpan.Zero)
                {
                    Cancel(throwOnFirstException: false);
                    return true;
                }

                CancelAfter(leaseTime);
            }
            catch (ObjectDisposedException)
            {
                return false;
            }

            return Token.IsCancellationRequested is false;
        }
    }

    private readonly TimeSpan maxLease;

    // Concurrency profile: multiple readers, single writer
    [SuppressMessage("Usage", "CA2213", Justification = "Disposed using DestroyLease() method")]
    private volatile Lease? lease; // null if disposed

    private void RenewLease(TimeSpan elapsed)
    {
        var currentLease = lease;
        if (currentLease is not null && currentLease.TryRenew(elapsed = maxLease - elapsed) is false)
        {
            var newLease = new Lease();
            if (ReferenceEquals(Interlocked.CompareExchange(ref lease, newLease, currentLease), currentLease))
            {
                newLease.CancelAfter(elapsed);
            }
            else
            {
                newLease.Dispose();
            }
        }
    }

    private void DestroyLease()
    {
        if (Interlocked.Exchange(ref lease, null) is { } disposable)
        {
            try
            {
                disposable.Cancel(throwOnFirstException: false);
            }
            finally
            {
                disposable.Dispose();
            }
        }
    }

    internal bool TryGetLeaseToken(out CancellationToken token)
    {
        if (lease is { } tokenSource)
        {
            token = tokenSource.Token;
            return true;
        }

        token = new(canceled: true);
        return false;
    }
}
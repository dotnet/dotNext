using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft;

internal partial class LeaderState<TMember>
{
    private sealed class LeaderLease : CancellationTokenSource, ILeaderLease
    {
        private readonly CancellationToken cachedToken; // cached to avoid ObjectDisposedException

        internal LeaderLease() => cachedToken = Token;

        CancellationToken ILeaderLease.Token => cachedToken;

        bool ILeaderLease.IsExpired => IsCancellationRequested;
    }

    private sealed class ExpiredLease : ILeaderLease
    {
        internal static readonly ExpiredLease Instance = new();

        private ExpiredLease()
        {
        }

        CancellationToken ILeaderLease.Token => new(canceled: true);

        bool ILeaderLease.IsExpired => true;
    }

    private readonly TimeSpan maxLease;
    private volatile ILeaderLease? lease; // null if disposed, ExpiredLease, or LeaderLease

    [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1013", Justification = "False positive")]
    private void RenewLease(TimeSpan elapsed)
    {
        var currentLease = lease;

        // lease is expired, create a new lease
        switch (currentLease)
        {
            case null: // lease is destroyed, just leave the method
                break;
            case { IsExpired: true }:
                goto default;
            case LeaderLease resettable: // reuse existing instance of CTS, if possible
                if (!resettable.TryReset())
                    goto default;

                resettable.CancelAfter(maxLease - elapsed);
                break;
            default:
                if (elapsed < maxLease)
                    Renew(maxLease - elapsed);

                break;
        }

        void Renew(TimeSpan leaseTime)
        {
            var newLease = new LeaderLease();
            if (ReferenceEquals(currentLease, Interlocked.CompareExchange(ref lease, newLease, currentLease)))
            {
                (currentLease as LeaderLease)?.Dispose();
                newLease.CancelAfter(leaseTime);
            }
            else
            {
                newLease.Dispose();
            }
        }
    }

    private void DestroyLease()
    {
        if (Interlocked.Exchange(ref lease, null) is LeaderLease disposable)
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

    internal ILeaderLease? Lease => lease;
}
using static System.Threading.Timeout;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Timestamp = Diagnostics.Timestamp;

internal partial class LeaderState<TMember>
{
    private sealed class LeaderLease : ILeaderLease
    {
        private readonly TimeSpan maxLease;
        private Timestamp createdAt; // volatile

        internal LeaderLease(Timestamp startTime, TimeSpan maxLease, CancellationToken leaseToken)
        {
            createdAt = startTime;
            this.maxLease = maxLease;
            Token = leaseToken;
        }

        internal bool TryReset(Timestamp startTime, out TimeSpan remainingTime)
        {
            Timestamp.VolatileWrite(ref createdAt, startTime);
            return (remainingTime = maxLease - startTime.Elapsed) > TimeSpan.Zero;
        }

        public bool IsExpired => Timestamp.VolatileRead(ref createdAt).Elapsed >= maxLease;

        public CancellationToken Token { get; }
    }

    private readonly TimeSpan maxLease;
    private readonly Timer leaseTimer;
    private CancellationTokenSource leaseTokenSource;
    private volatile LeaderLease? lease;

    private object SyncRoot => timerCancellation;

    private void RenewLease(Timestamp startTime)
    {
        if (TryReset(out var leaseTime) && leaseTimer.Change(leaseTime, InfiniteTimeSpan))
        {
            var tokenSourceToDispose = default(IDisposable);
            Monitor.Enter(SyncRoot);
            try
            {
                if (leaseTokenSource.IsCancellationRequested)
                {
                    tokenSourceToDispose = leaseTokenSource;
                    leaseTokenSource = CancellationTokenSource.CreateLinkedTokenSource(LeadershipToken);
                    lease = new(startTime, maxLease, leaseTokenSource.Token);
                }
            }
            finally
            {
                Monitor.Exit(SyncRoot);
                tokenSourceToDispose?.Dispose();
            }
        }

        bool TryReset(out TimeSpan leaseTime)
        {
            LeaderLease? lease = this.lease;

            if (lease is null)
            {
                Debug.Assert(leaseTokenSource.IsCancellationRequested);

                leaseTime = maxLease;
                return true;
            }

            return lease.TryReset(startTime, out leaseTime);
        }
    }

    private void OnLeaseExpired()
    {
        if (Monitor.TryEnter(SyncRoot))
        {
            try
            {
                leaseTokenSource.Cancel();
            }
            finally
            {
                Monitor.Exit(SyncRoot);
            }
        }
    }

    internal ILeaderLease? Lease => lease;
}
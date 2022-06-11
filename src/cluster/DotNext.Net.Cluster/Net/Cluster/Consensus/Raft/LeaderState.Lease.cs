using static System.Threading.Timeout;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Timestamp = Diagnostics.Timestamp;
using BoxedCancellationToken = Runtime.BoxedValue<CancellationToken>;

internal partial class LeaderState : ILeaderLease
{
    private readonly TimeSpan maxLease;
    private readonly Timer leaseTimer;
    private CancellationTokenSource leaseTokenSource;

    // cached token from leaseTokenSource to avoid ObjectDisposedException
    private volatile BoxedCancellationToken leaseToken;

    private void RenewLease(Timestamp startTime)
    {
        var leaseTime = maxLease - startTime.Elapsed;
        if (leaseTime > TimeSpan.Zero && leaseTimer.Change(leaseTime, InfiniteTimeSpan))
        {
            lock (leaseTimer)
            {
                var prevTokenSource = leaseTokenSource;
                if (prevTokenSource.IsCancellationRequested)
                {
                    leaseToken = BoxedCancellationToken.Box((leaseTokenSource = CancellationTokenSource.CreateLinkedTokenSource(LeadershipToken)).Token);
                    prevTokenSource.Dispose();
                }
            }
        }
    }

    private void OnLeaseExpired()
    {
        if (Monitor.TryEnter(leaseTimer))
        {
            try
            {
                leaseTokenSource.Cancel();
            }
            finally
            {
                Monitor.Exit(leaseTimer);
            }
        }
    }

    CancellationToken ILeaderLease.Token => leaseToken.Value;
}
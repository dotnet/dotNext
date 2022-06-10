namespace DotNext.Net.Cluster.Consensus.Raft;

using Timestamp = Diagnostics.Timestamp;

internal partial class LeaderState : ILeaderLease
{
    private readonly TimeSpan maxLease;
    private CancellationTokenSource leaseTokenSource;

    private void RenewLease(Timestamp startTime)
    {
        var leaseTime = maxLease - startTime.Elapsed;
        if (leaseTime > TimeSpan.Zero)
        {
            // attempt to reuse token source
            if (!leaseTokenSource.TryReset())
            {
                using (leaseTokenSource)
                {
                    // lease expired
                    leaseTokenSource = new();
                }
            }

            leaseTokenSource.CancelAfter(leaseTime);
        }
        else
        {
            leaseTokenSource.Cancel();
        }
    }

    CancellationToken ILeaderLease.Token
    {
        get
        {
            CancellationToken result;

            try
            {
                result = leaseTokenSource.Token;
            }
            catch (ObjectDisposedException)
            {
                result = new(true);
            }

            return result;
        }
    }
}
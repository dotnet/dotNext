using Microsoft.Extensions.Logging;
using static System.Threading.Timeout;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft;

using IO.Log;
using Membership;
using Threading.Tasks;
using static Threading.LinkedTokenSourceFactory;
using Timestamp = Diagnostics.Timestamp;

internal sealed partial class LeaderState : RaftState
{
    private const int MaxTermCacheSize = 100;
    private readonly long currentTerm;
    private readonly bool allowPartitioning;
    private readonly CancellationTokenSource timerCancellation;
    internal readonly CancellationToken LeadershipToken; // cached to avoid ObjectDisposedException

    private Task? heartbeatTask;
    internal ILeaderStateMetrics? Metrics;

    internal LeaderState(IRaftStateMachine stateMachine, bool allowPartitioning, long term, TimeSpan maxLease)
        : base(stateMachine)
    {
        currentTerm = term;
        this.allowPartitioning = allowPartitioning;
        timerCancellation = new();
        LeadershipToken = timerCancellation.Token;
        (leaseTokenSource = new()).Cancel();
        precedingTermCache = new(MaxTermCacheSize);
        this.maxLease = maxLease;
        leaseTimer = new(OnLeaseExpired, new WeakReference<LeaderState>(this), InfiniteTimeSpan, InfiniteTimeSpan);

        static void OnLeaseExpired(object? state)
        {
            if ((state as WeakReference<LeaderState>)?.TryGetTarget(out var leader) ?? false)
                leader.OnLeaseExpired();
        }
    }

    private async Task<bool> DoHeartbeats(Timestamp startTime, TaskCompletionPipe<Task<Result<bool>>> responsePipe, IAuditTrail<IRaftLogEntry> auditTrail, IClusterConfigurationStorage configurationStorage, CancellationToken token)
    {
        long commitIndex = auditTrail.LastCommittedEntryIndex,
            currentIndex = auditTrail.LastUncommittedEntryIndex,
            term = currentTerm,
            minPrecedingIndex = 0L;

        var activeConfig = configurationStorage.ActiveConfiguration;
        var proposedConfig = configurationStorage.ProposedConfiguration;

        var leaseRenewalThreshold = 0;

        // send heartbeat in parallel
        foreach (var member in Members)
        {
            leaseRenewalThreshold++;

            if (member.IsRemote)
            {
                long precedingIndex = Math.Max(0, member.NextIndex - 1), precedingTerm;
                minPrecedingIndex = Math.Min(minPrecedingIndex, precedingIndex);

                // try to get term from the cache to avoid touching audit trail for each member
                if (!precedingTermCache.TryGetValue(precedingIndex, out precedingTerm))
                    precedingTermCache.Add(precedingIndex, precedingTerm = await auditTrail.GetTermAsync(precedingIndex, token).ConfigureAwait(false));

                responsePipe.Add(new Replicator(auditTrail, activeConfig, proposedConfig, member, commitIndex, currentIndex, term, precedingIndex, precedingTerm, Logger, token).ReplicateAsync());
            }
        }

        responsePipe.Complete();

        // clear cache
        if (precedingTermCache.Count > MaxTermCacheSize)
            precedingTermCache.Clear();
        else
            precedingTermCache.RemoveHead(minPrecedingIndex);

        // update lease if the cluster contains only one local node
        if (leaseRenewalThreshold is 1)
            RenewLease(startTime);
        else
            leaseRenewalThreshold = (leaseRenewalThreshold / 2) + 1;

        int quorum = 1, commitQuorum = 1; // because we know that the entry is replicated in this node
        await foreach (var task in responsePipe.ConfigureAwait(false))
        {
            Debug.Assert(task.IsCompleted);

            try
            {
                var result = task.GetAwaiter().GetResult();
                term = Math.Max(term, result.Term);
                quorum++;

                if (result.Value)
                {
                    if (--leaseRenewalThreshold is 0)
                        RenewLease(startTime);

                    commitQuorum++;
                }
                else
                {
                    commitQuorum--;
                }
            }
            catch (MemberUnavailableException)
            {
                quorum -= 1;
                commitQuorum -= 1;
            }
            catch (OperationCanceledException)
            {
                // leading was canceled
                Metrics?.ReportBroadcastTime(startTime.Elapsed);
                return false;
            }
            catch (Exception e)
            {
                Logger.LogError(e, ExceptionMessages.UnexpectedError);
            }
        }

        Metrics?.ReportBroadcastTime(startTime.Elapsed);

        if (term <= currentTerm && (quorum > 0 || allowPartitioning))
        {
            Debug.Assert(quorum >= commitQuorum);

            if (commitQuorum > 0)
            {
                // majority of nodes accept entries with at least one entry from the current term
                var count = await auditTrail.CommitAsync(currentIndex, token).ConfigureAwait(false); // commit all entries starting from the first uncommitted index to the end
                Logger.CommitSuccessful(commitIndex + 1, count);
            }
            else
            {
                Logger.CommitFailed(quorum, commitIndex);
            }

            await configurationStorage.ApplyAsync(token).ConfigureAwait(false);
            UpdateLeaderStickiness();
            return true;
        }

        // it is partitioned network with absolute majority, not possible to have more than one leader
        MoveToFollowerState(randomizeTimeout: false, term);
        return false;
    }

    private async Task DoHeartbeats(TimeSpan period, IAuditTrail<IRaftLogEntry> auditTrail, IClusterConfigurationStorage configurationStorage, CancellationToken token)
    {
        using var cancellationSource = token.LinkTo(LeadershipToken);
        var responsePipe = CreatePipe(Members.Count);
        await Task.Yield(); // unblock the caller

        for (var forced = false; ; responsePipe.Reset())
        {
            var startTime = new Timestamp();
            if (!await DoHeartbeats(startTime, responsePipe, auditTrail, configurationStorage, token).ConfigureAwait(false))
                break;

            if (forced)
                DrainReplicationQueue();

            // subtract heartbeat processing duration from heartbeat period for better stability
            var delay = period - startTime.Elapsed;
            forced = await WaitForReplicationAsync(delay > TimeSpan.Zero ? delay : TimeSpan.Zero, token).ConfigureAwait(false);
        }

        static TaskCompletionPipe<Task<Result<bool>>> CreatePipe(int capacity)
        {
            if (capacity < int.MaxValue / 2)
                capacity *= 2;

            return new(capacity);
        }
    }

    /// <summary>
    /// Starts cluster synchronization.
    /// </summary>
    /// <param name="period">Time period of Heartbeats.</param>
    /// <param name="transactionLog">Transaction log.</param>
    /// <param name="configurationStorage">Cluster configuration storage.</param>
    /// <param name="token">The toke that can be used to cancel the operation.</param>
    internal void StartLeading(TimeSpan period, IAuditTrail<IRaftLogEntry> transactionLog, IClusterConfigurationStorage configurationStorage, CancellationToken token)
    {
        foreach (var member in Members)
        {
            member.NextIndex = transactionLog.LastUncommittedEntryIndex + 1;
            member.ConfigurationFingerprint = 0L;
        }

        heartbeatTask = DoHeartbeats(period, transactionLog, configurationStorage, token);
    }

    private void Cleanup()
    {
        timerCancellation.Dispose();
        heartbeatTask = null;

        lease = null;
        leaseTimer.Dispose();
        leaseTokenSource.Dispose();

        // cancel replication queue
        replicationQueue.Dispose(new InvalidOperationException(ExceptionMessages.LocalNodeNotLeader));
        replicationEvent.Dispose();

        Metrics = null;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        try
        {
            timerCancellation.Cancel(false);
            replicationEvent.CancelSuspendedCallers(LeadershipToken);
            await leaseTimer.DisposeAsync().ConfigureAwait(false);
            await (heartbeatTask ?? Task.CompletedTask).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Logger.LeaderStateExitedWithError(e);
        }
        finally
        {
            Cleanup();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Cleanup();
        }

        base.Dispose(disposing);
    }
}
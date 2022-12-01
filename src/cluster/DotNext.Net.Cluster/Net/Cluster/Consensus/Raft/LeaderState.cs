using Microsoft.Extensions.Logging;
using static System.Threading.Timeout;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft;

using IO.Log;
using Membership;
using Threading.Tasks;
using static Threading.LinkedTokenSourceFactory;
using Timestamp = Diagnostics.Timestamp;
using GCLatencyModeScope = Runtime.GCLatencyModeScope;

internal sealed partial class LeaderState<TMember> : RaftState<TMember>
    where TMember : class, IRaftClusterMember
{
    private const int MaxTermCacheSize = 100;
    private readonly long currentTerm;
    private readonly bool allowPartitioning;
    private readonly CancellationTokenSource timerCancellation;
    internal readonly CancellationToken LeadershipToken; // cached to avoid ObjectDisposedException

    private Task? heartbeatTask;

    internal LeaderState(IRaftStateMachine<TMember> stateMachine, bool allowPartitioning, long term, TimeSpan maxLease)
        : base(stateMachine)
    {
        currentTerm = term;
        this.allowPartitioning = allowPartitioning;
        timerCancellation = new();
        LeadershipToken = timerCancellation.Token;
        (leaseTokenSource = new()).Cancel();
        precedingTermCache = new(MaxTermCacheSize);
        this.maxLease = maxLease;
        leaseTimer = new(OnLeaseExpired, new WeakReference<LeaderState<TMember>>(this), InfiniteTimeSpan, InfiniteTimeSpan);

        static void OnLeaseExpired(object? state)
        {
            if ((state as WeakReference<LeaderState<TMember>>)?.TryGetTarget(out var leader) ?? false)
                leader.OnLeaseExpired();
        }
    }

    internal ILeaderStateMetrics? Metrics
    {
        private get;
        init;
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

                // fork replication procedure
                responsePipe.Add(QueueReplication(new Replicator(auditTrail, activeConfig, proposedConfig, member, commitIndex, currentIndex, term, precedingIndex, precedingTerm, Logger, token)));
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
            leaseRenewalThreshold = (leaseRenewalThreshold >> 1) + 1;

        int quorum = 1, commitQuorum = 1; // because we know that the entry is replicated in this node
        await foreach (var task in responsePipe.ConfigureAwait(false))
        {
            var member = ReplicationWorkItem.GetReplicatedMember(task);
            Debug.Assert(member is not null);
            Debug.Assert(task.IsCompleted);

            try
            {
                var result = task.GetAwaiter().GetResult();
                failureDetector?.ReportHeartbeat(member);
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

            // report unavailable cluster member
            if ((failureDetector?.IsAlive(member) ?? true) is false)
                UnavailableMemberDetected(member, LeadershipToken);
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
        await Task.Yield(); // unblock the caller

        var forced = false;
        for (var responsePipe = new TaskCompletionPipe<Task<Result<bool>>>(); !token.IsCancellationRequested; responsePipe.Reset())
        {
            var startTime = new Timestamp();

            // we want to minimize GC intrusion during replication process
            // (however, it is still allowed in case of system-wide memory pressure, e.g. due to container limits)
            using (GCLatencyModeScope.SustainedLowLatency)
            {
                if (!await DoHeartbeats(startTime, responsePipe, auditTrail, configurationStorage, token).ConfigureAwait(false))
                    break;
            }

            if (forced)
                DrainReplicationQueue();

            // subtract heartbeat processing duration from heartbeat period for better stability
            var delay = period - startTime.Elapsed;
            forced = await WaitForReplicationAsync(delay > TimeSpan.Zero ? delay : TimeSpan.Zero, token).ConfigureAwait(false);
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

        failureDetector?.Clear();
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        try
        {
            timerCancellation.Cancel(false);
            replicationEvent.CancelSuspendedCallers(LeadershipToken);
            await leaseTimer.DisposeAsync().ConfigureAwait(false);
            await (heartbeatTask ?? Task.CompletedTask).ConfigureAwait(false); // may throw OperationCanceledException
        }
        catch (OperationCanceledException) when (heartbeatTask?.IsCanceled ?? true)
        {
            // suspend cancellation
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
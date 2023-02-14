using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft;

using IO.Log;
using Membership;
using Runtime.CompilerServices;
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
        this.maxLease = maxLease;
        lease = ExpiredLease.Instance;
    }

    internal ILeaderStateMetrics? Metrics
    {
        private get;
        init;
    }

    // no need to allocate state machine for every round of heartbeats
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<bool> DoHeartbeats(Timestamp startTime, TaskCompletionPipe<Task<Result<bool>>> responsePipe, IAuditTrail<IRaftLogEntry> auditTrail, IClusterConfigurationStorage configurationStorage, CancellationToken token)
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
                if (!precedingTermCache.TryGet(precedingIndex, out precedingTerm))
                    precedingTermCache.Add(precedingIndex, precedingTerm = await auditTrail.GetTermAsync(precedingIndex, token).ConfigureAwait(false));

                // fork replication procedure
                responsePipe.Add(QueueReplication(new(activeConfig, proposedConfig, member, commitIndex, term, precedingIndex, precedingTerm, Logger), auditTrail, currentIndex, token));
            }
        }

        responsePipe.Complete();

        // Clear cache:
        // 1. Best case: remove all entries from the cache up to the minimal observed index (those entries will never be requested)
        // 2. Worst case: cleanup the entire cache because one of the members too far behind of the leader (perhaps, it's unavailable)
        if (precedingTermCache.ApproximatedCount < MaxTermCacheSize)
            precedingTermCache.RemovePriorTo(minPrecedingIndex);
        else
            precedingTermCache.Clear();

        // update lease if the cluster contains only one local node
        if (leaseRenewalThreshold is 1)
            RenewLease(startTime.Elapsed);
        else
            leaseRenewalThreshold = (leaseRenewalThreshold >> 1) + 1;

        int quorum = 1, commitQuorum = 1; // because we know that the entry is replicated in this node
        while (await responsePipe.WaitToReadAsync(token).ConfigureAwait(false))
        {
            while (responsePipe.TryRead(out var response))
            {
                if (!ProcessMemberResponse(startTime, response, ref term, ref quorum, ref commitQuorum, ref leaseRenewalThreshold))
                    return false;
            }
        }

        var broadcastTime = startTime.ElapsedMilliseconds;
        Metrics?.ReportBroadcastTime(TimeSpan.FromMilliseconds(broadcastTime));
        LeaderState.BroadcastTimeMeter.Record(broadcastTime, MeasurementTags);

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

    private bool ProcessMemberResponse(Timestamp startTime, Task<Result<bool>> response, ref long term, ref int quorum, ref int commitQuorum, ref int leaseRenewalThreshold)
    {
        var member = ReplicationWorkItem.GetReplicatedMember(response);

        try
        {
            var result = response.GetAwaiter().GetResult();
            failureDetector?.ReportHeartbeat(member);
            term = Math.Max(term, result.Term);
            quorum++;

            if (result.Value)
            {
                if (--leaseRenewalThreshold is 0)
                    RenewLease(startTime.Elapsed);

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
            var broadcastTime = startTime.ElapsedMilliseconds;
            Metrics?.ReportBroadcastTime(TimeSpan.FromMilliseconds(broadcastTime));
            LeaderState.BroadcastTimeMeter.Record(broadcastTime, MeasurementTags);

            return false;
        }
        catch (Exception e)
        {
            Logger.LogError(e, ExceptionMessages.UnexpectedError);
        }
        finally
        {
            response.Dispose();
        }

        // report unavailable cluster member
        if (failureDetector is not null && failureDetector.IsAlive(member) is false)
            UnavailableMemberDetected(member, LeadershipToken);

        return true;
    }

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task DoHeartbeats(TimeSpan period, IAuditTrail<IRaftLogEntry> auditTrail, IClusterConfigurationStorage configurationStorage, CancellationToken token)
    {
        using var cancellationSource = token.LinkTo(LeadershipToken);

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

    protected override async ValueTask DisposeAsyncCore()
    {
        try
        {
            timerCancellation.Cancel(false);
            replicationEvent.CancelSuspendedCallers(LeadershipToken);
            await (heartbeatTask ?? Task.CompletedTask).ConfigureAwait(false); // may throw OperationCanceledException
        }
        catch (Exception e)
        {
            Logger.LeaderStateExitedWithError(e);
        }
        finally
        {
            Dispose(disposing: true);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timerCancellation.Dispose();
            heartbeatTask = null;

            DestroyLease();

            // cancel replication queue
            replicationQueue.Dispose(new InvalidOperationException(ExceptionMessages.LocalNodeNotLeader));
            replicationEvent.Dispose();

            failureDetector?.Clear();
            precedingTermCache.Clear();
        }

        base.Dispose(disposing);
    }
}

internal static class LeaderState
{
    internal static readonly Histogram<double> BroadcastTimeMeter = Metrics.Instrumentation.ServerSide.CreateHistogram<double>("broadcast-time", unit: "ms");
    internal static readonly Counter<int> TransitionRateMeter = Metrics.Instrumentation.ServerSide.CreateCounter<int>("transitions-to-leader-count");
}
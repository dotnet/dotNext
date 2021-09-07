using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO.Log;
    using Membership;
    using static Threading.LinkedTokenSourceFactory;
    using static Threading.Tasks.Continuation;
    using AsyncResultSet = Buffers.PooledArrayBufferWriter<Task<Result<bool>>>;
    using Timestamp = Diagnostics.Timestamp;

    internal sealed partial class LeaderState : RaftState, ILeaderLease
    {
        private const int MaxTermCacheSize = 100;
        private readonly long currentTerm;
        private readonly bool allowPartitioning;
        private readonly CancellationTokenSource timerCancellation;
        internal readonly CancellationToken LeadershipToken; // cached to avoid ObjectDisposedException

        // key is log entry index, value is log entry term
        private readonly TermCache precedingTermCache;
        private readonly TimeSpan maxLease;
        private Timestamp replicatedAt;
        private Task? heartbeatTask;
        internal ILeaderStateMetrics? Metrics;

        internal LeaderState(IRaftStateMachine stateMachine, bool allowPartitioning, long term, TimeSpan maxLease)
            : base(stateMachine)
        {
            currentTerm = term;
            this.allowPartitioning = allowPartitioning;
            timerCancellation = new();
            LeadershipToken = timerCancellation.Token;
            replicationEvent = new();
            replicationQueue = new(TaskCreationOptions.RunContinuationsAsynchronously);
            precedingTermCache = new TermCache(MaxTermCacheSize);
            this.maxLease = maxLease;
        }

        private async Task<bool> DoHeartbeats(AsyncResultSet taskBuffer, IAuditTrail<IRaftLogEntry> auditTrail, IClusterConfigurationStorage configurationStorage, CancellationToken token)
        {
            var start = Timestamp.Current;
            long commitIndex = auditTrail.GetLastIndex(true),
                currentIndex = auditTrail.GetLastIndex(false),
                term = currentTerm,
                minPrecedingIndex = 0L;

            var activeConfig = configurationStorage.ActiveConfiguration;
            var proposedConfig = configurationStorage.ProposedConfiguration;

            var leaseRenewalThreshold = 0;

            // send heartbeat in parallel
            foreach (var member in stateMachine.Members)
            {
                leaseRenewalThreshold++;

                if (member.IsRemote)
                {
                    long precedingIndex = Math.Max(0, member.NextIndex - 1), precedingTerm;
                    minPrecedingIndex = Math.Min(minPrecedingIndex, precedingIndex);

                    // try to get term from the cache to avoid touching audit trail for each member
                    if (!precedingTermCache.TryGetValue(precedingIndex, out precedingTerm))
                        precedingTermCache.Add(precedingIndex, precedingTerm = await auditTrail.GetTermAsync(precedingIndex, token).ConfigureAwait(false));

                    taskBuffer.Add(new Replicator(auditTrail, activeConfig, proposedConfig, member, commitIndex, currentIndex, term, precedingIndex, precedingTerm, stateMachine.Logger, token).ReplicateAsync());
                }
            }

            // clear cache
            if (precedingTermCache.Count > MaxTermCacheSize)
                precedingTermCache.Clear();
            else
                precedingTermCache.RemoveHead(minPrecedingIndex);

            leaseRenewalThreshold = (leaseRenewalThreshold / 2) + 1;

            int quorum = 1, commitQuorum = 1; // because we know that the entry is replicated in this node
            foreach (var task in taskBuffer)
            {
                try
                {
                    var result = await task.ConfigureAwait(false);
                    term = Math.Max(term, result.Term);
                    quorum++;

                    if (result.Value)
                    {
                        if (--leaseRenewalThreshold == 0)
                            Timestamp.VolatileWrite(ref replicatedAt, start + maxLease); // renew lease

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
                    Metrics?.ReportBroadcastTime(start.Elapsed);
                    return false;
                }
                catch (Exception e)
                {
                    stateMachine.Logger.LogError(e, ExceptionMessages.UnexpectedError);
                }
            }

            Metrics?.ReportBroadcastTime(start.Elapsed);

            if (term <= currentTerm && (quorum > 0 || allowPartitioning))
            {
                Debug.Assert(quorum >= commitQuorum);

                if (commitQuorum > 0)
                {
                    // majority of nodes accept entries with at least one entry from the current term
                    var count = await auditTrail.CommitAsync(currentIndex, token).ConfigureAwait(false); // commit all entries starting from the first uncommitted index to the end
                    stateMachine.Logger.CommitSuccessful(commitIndex + 1, count);
                }
                else
                {
                    stateMachine.Logger.CommitFailed(quorum, commitIndex);
                }

                await configurationStorage.ApplyAsync(token).ConfigureAwait(false);
                return true;
            }

            // it is partitioned network with absolute majority, not possible to have more than one leader
            stateMachine.MoveToFollowerState(false, term);
            return false;
        }

        private async Task DoHeartbeats(TimeSpan period, IAuditTrail<IRaftLogEntry> auditTrail, IClusterConfigurationStorage configurationStorage, CancellationToken token)
        {
            using var cancellationSource = token.LinkTo(LeadershipToken);

            // reuse this buffer to place responses from other nodes
            using var taskBuffer = new AsyncResultSet(stateMachine.Members.Count);

            for (var forced = false; await DoHeartbeats(taskBuffer, auditTrail, configurationStorage, token).ConfigureAwait(false); forced = await WaitForReplicationAsync(period, token).ConfigureAwait(false))
            {
                if (forced)
                    DrainReplicationQueue();

                taskBuffer.Clear(true);
            }
        }

        /// <summary>
        /// Starts cluster synchronization.
        /// </summary>
        /// <param name="period">Time period of Heartbeats.</param>
        /// <param name="transactionLog">Transaction log.</param>
        /// <param name="configurationStorage">Cluster configuration storage.</param>
        /// <param name="token">The toke that can be used to cancel the operation.</param>
        internal LeaderState StartLeading(TimeSpan period, IAuditTrail<IRaftLogEntry> transactionLog, IClusterConfigurationStorage configurationStorage, CancellationToken token)
        {
            foreach (var member in stateMachine.Members)
            {
                member.NextIndex = transactionLog.GetLastIndex(false) + 1;
                member.ConfigurationFingerprint = 0L;
            }

            heartbeatTask = DoHeartbeats(period, transactionLog, configurationStorage, token);
            return this;
        }

        bool ILeaderLease.IsExpired
            => LeadershipToken.IsCancellationRequested || Timestamp.VolatileRead(ref replicatedAt) < Timestamp.Current;

        internal override Task StopAsync()
        {
            timerCancellation.Cancel(false);
            replicationEvent.CancelSuspendedCallers(timerCancellation.Token);
            return heartbeatTask?.OnCompleted() ?? Task.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timerCancellation.Dispose();
                heartbeatTask = null;

                // cancel replication queue
                replicationQueue.TrySetException(new InvalidOperationException(ExceptionMessages.LocalNodeNotLeader));
                replicationEvent.Dispose();

                Metrics = null;
            }

            base.Dispose(disposing);
        }
    }
}

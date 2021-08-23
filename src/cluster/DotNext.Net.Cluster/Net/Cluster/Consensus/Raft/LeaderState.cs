using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO.Log;
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

        // key is log entry index, value is log entry term
        private readonly TermCache precedingTermCache;
        private readonly TimeSpan maxLease;
        private Timestamp replicatedAt;
        private Task? heartbeatTask;
        internal ILeaderStateMetrics? Metrics;
        internal readonly CancellationToken LeadershipToken; // cached to avoid ObjectDisposedException

        internal LeaderState(IRaftStateMachine stateMachine, bool allowPartitioning, long term, TimeSpan maxLease)
            : base(stateMachine)
        {
            currentTerm = term;
            this.allowPartitioning = allowPartitioning;
            timerCancellation = new();
            LeadershipToken = timerCancellation.Token;
            replicationEvent = new();
            replicationQueue = new();
            precedingTermCache = new TermCache(MaxTermCacheSize);
            this.maxLease = maxLease;
        }

        private async Task<bool> DoHeartbeats(AsyncResultSet taskBuffer, IAuditTrail<IRaftLogEntry> auditTrail, CancellationToken token)
        {
            var timeStamp = Timestamp.Current;
            long commitIndex = auditTrail.GetLastIndex(true),
                currentIndex = auditTrail.GetLastIndex(false),
                term = currentTerm,
                minPrecedingIndex = 0L;

            // send heartbeat in parallel
            foreach (var member in stateMachine.Members)
            {
                if (member.IsRemote)
                {
                    long precedingIndex = Math.Max(0, member.NextIndex - 1), precedingTerm;
                    minPrecedingIndex = Math.Min(minPrecedingIndex, precedingIndex);

                    // try to get term from the cache to avoid touching audit trail for each member
                    if (!precedingTermCache.TryGetValue(precedingIndex, out precedingTerm))
                        precedingTermCache.Add(precedingIndex, precedingTerm = await auditTrail.GetTermAsync(precedingIndex, token).ConfigureAwait(false));

                    taskBuffer.Add(new Replicator(auditTrail, member, commitIndex, currentIndex, term, precedingIndex, precedingTerm, stateMachine.Logger, token).ReplicateAsync());
                }
            }

            // clear cache
            if (precedingTermCache.Count > MaxTermCacheSize)
                precedingTermCache.Clear();
            else
                precedingTermCache.RemoveHead(minPrecedingIndex);

            int quorum = 1, commitQuorum = 1; // because we know that the entry is replicated in this node
            foreach (var task in taskBuffer)
            {
                try
                {
                    var result = await task.ConfigureAwait(false);
                    term = Math.Max(term, result.Term);
                    quorum += 1;
                    commitQuorum += result.Value ? 1 : -1;
                }
                catch (MemberUnavailableException)
                {
                    quorum -= 1;
                    commitQuorum -= 1;
                }
                catch (OperationCanceledException)
                {
                    // leading was canceled
                    Metrics?.ReportBroadcastTime(timeStamp.Elapsed);
                    return false;
                }
                catch (Exception e)
                {
                    stateMachine.Logger.LogError(e, ExceptionMessages.UnexpectedError);
                }
            }

            Metrics?.ReportBroadcastTime(timeStamp.Elapsed);

            // majority of nodes accept entries with a least one entry from the current term
            if (commitQuorum > 0)
            {
                var count = await auditTrail.CommitAsync(currentIndex, token).ConfigureAwait(false); // commit all entries starting from the first uncommitted index to the end
                stateMachine.Logger.CommitSuccessful(commitIndex + 1, count);
                Timestamp.VolatileWrite(ref replicatedAt, Timestamp.Current); // renew lease
                goto check_term;
            }

            stateMachine.Logger.CommitFailed(quorum, commitIndex);

            // majority of nodes replicated, continue leading if current term is not changed
            if (quorum <= 0 && !allowPartitioning)
                goto stop_leading;

            check_term:
            if (term <= currentTerm)
                return true;

            // it is partitioned network with absolute majority, not possible to have more than one leader
            stop_leading:
            stateMachine.MoveToFollowerState(false, term);
            return false;
        }

        private async Task DoHeartbeats(TimeSpan period, IAuditTrail<IRaftLogEntry> auditTrail, CancellationToken token)
        {
            using var cancellationSource = token.LinkTo(LeadershipToken);

            // reuse this buffer to place responses from other nodes
            using var taskBuffer = new AsyncResultSet(stateMachine.Members.Count);

            for (var forced = false; await DoHeartbeats(taskBuffer, auditTrail, token).ConfigureAwait(false); forced = await WaitForReplicationAsync(period, token).ConfigureAwait(false))
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
        /// <param name="token">The toke that can be used to cancel the operation.</param>
        internal LeaderState StartLeading(TimeSpan period, IAuditTrail<IRaftLogEntry> transactionLog, CancellationToken token)
        {
            foreach (var member in stateMachine.Members)
                member.NextIndex = transactionLog.GetLastIndex(false) + 1;
            heartbeatTask = DoHeartbeats(period, transactionLog, token);
            return this;
        }

        bool ILeaderLease.IsExpired
            => LeadershipToken.IsCancellationRequested || Timestamp.VolatileRead(ref replicatedAt).Elapsed > maxLease;

        internal override Task StopAsync()
        {
            timerCancellation.Cancel(false);
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

                Metrics = null;
            }

            base.Dispose(disposing);
        }
    }
}

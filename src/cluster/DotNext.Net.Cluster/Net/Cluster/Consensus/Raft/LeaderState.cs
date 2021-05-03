using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO.Log;
    using static Threading.Tasks.Continuation;
    using Timestamp = Diagnostics.Timestamp;

    internal sealed partial class LeaderState : RaftState
    {
        private sealed class AsyncResultSet : LinkedList<ValueTask<Result<bool>>>
        {
        }

        private const int MaxTermCacheSize = 100;
        private readonly long currentTerm;
        private readonly bool allowPartitioning;
        private readonly CancellationTokenSource timerCancellation;

        // key is log entry index, value is log entry term
        private readonly TermCache precedingTermCache;
        private Task? heartbeatTask;
        internal ILeaderStateMetrics? Metrics;

        internal LeaderState(IRaftStateMachine stateMachine, bool allowPartitioning, long term)
            : base(stateMachine)
        {
            currentTerm = term;
            this.allowPartitioning = allowPartitioning;
            timerCancellation = new();
            replicationEvent = new();
            replicationQueue = new();
            precedingTermCache = new TermCache(stateMachine.Members.Count);
        }

        private async Task<bool> DoHeartbeats(IAuditTrail<IRaftLogEntry> auditTrail, CancellationToken token)
        {
            var timeStamp = Timestamp.Current;
            var tasks = new AsyncResultSet();

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

                    tasks.AddLast(new Replicator(member, commitIndex, currentIndex, term, precedingIndex, precedingTerm, stateMachine.Logger, token).Start(auditTrail));
                }
            }

            // clear cache
            if (precedingTermCache.Count > MaxTermCacheSize)
                precedingTermCache.Clear();
            else
                precedingTermCache.RemoveHead(minPrecedingIndex);

            int quorum = 1, commitQuorum = 1; // because we know that the entry is replicated in this node
            for (var task = tasks.First; task is not null; task.Value = default, task = task.Next)
            {
                try
                {
                    var result = await task.Value.ConfigureAwait(false);
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
                    tasks.Clear();
                    Metrics?.ReportBroadcastTime(timeStamp.Elapsed);
                    return false;
                }
                catch (Exception e)
                {
                    stateMachine.Logger.LogError(e, ExceptionMessages.UnexpectedError);
                }
            }

            tasks.Clear();
            Metrics?.ReportBroadcastTime(timeStamp.Elapsed);

            // majority of nodes accept entries with a least one entry from the current term
            if (commitQuorum > 0)
            {
                var count = await auditTrail.CommitAsync(currentIndex, token).ConfigureAwait(false); // commit all entries started from first uncommitted index to the end
                stateMachine.Logger.CommitSuccessful(commitIndex + 1, count);
                goto check_term;
            }

            stateMachine.Logger.CommitFailed(quorum, commitIndex);

            // majority of nodes replicated, continue leading if current term is not changed
            if (quorum <= 0 & !allowPartitioning)
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
            using var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(token, timerCancellation.Token);
            token = cancellationSource.Token;
            for (var forced = false; await DoHeartbeats(auditTrail, token).ConfigureAwait(false); forced = await WaitForReplicationAsync(period, token).ConfigureAwait(false))
            {
                if (forced)
                    DrainReplicationQueue();
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

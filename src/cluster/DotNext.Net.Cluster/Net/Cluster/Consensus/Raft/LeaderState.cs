using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO.Log;
    using Threading;
    using static Threading.Tasks.Continuation;
    using static Threading.Tasks.Synchronization;
    using Timestamp = Diagnostics.Timestamp;

    internal sealed class LeaderState : RaftState
    {
        private sealed class Replicator : TaskCompletionSource<Result<bool>>, ILogEntryConsumer<IRaftLogEntry, Result<bool>>
        {
            private readonly IRaftClusterMember member;
            private readonly long commitIndex, precedingIndex, precedingTerm, term;
            private readonly ILogger logger;
            private readonly CancellationToken token;

            // state
            private long currentIndex;
            private bool replicatedWithCurrentTerm;
            private ConfiguredTaskAwaitable<Result<bool>>.ConfiguredTaskAwaiter replicationAwaiter;

            internal Replicator(
                IRaftClusterMember member,
                long commitIndex,
                long currentIndex,
                long term,
                long precedingIndex,
                long precedingTerm,
                ILogger logger,
                CancellationToken token)
            {
                this.member = member;
                this.precedingIndex = precedingIndex;
                this.precedingTerm = precedingTerm;
                this.commitIndex = commitIndex;
                this.currentIndex = currentIndex;
                this.term = term;
                this.logger = logger;
                this.token = token;
            }

            internal ValueTask<Result<bool>> Start(IAuditTrail<IRaftLogEntry> auditTrail)
            {
                logger.ReplicationStarted(member.EndPoint, currentIndex);
                return currentIndex >= member.NextIndex ?
                    auditTrail.ReadAsync(this, member.NextIndex, token) :
                    ReadAsync<IRaftLogEntry, IRaftLogEntry[]>(Array.Empty<IRaftLogEntry>(), null, token);
            }

            private void Complete()
            {
                try
                {
                    var result = replicationAwaiter.GetResult();
                    replicationAwaiter = default;

                    // analyze result and decrease node index when it is out-of-sync with the current node
                    if (result.Value)
                    {
                        logger.ReplicationSuccessful(member.EndPoint, member.NextIndex);
                        member.NextIndex.VolatileWrite(currentIndex + 1);
                        result = result.SetValue(replicatedWithCurrentTerm);
                    }
                    else
                    {
                        logger.ReplicationFailed(member.EndPoint, member.NextIndex.UpdateAndGet(static index => index > 0L ? index - 1L : index));
                    }

                    SetResult(result);
                }
                catch (Exception e)
                {
                    SetException(e);
                }
            }

            public ValueTask<Result<bool>> ReadAsync<TEntry, TList>(TList entries, long? snapshotIndex, CancellationToken token)
                where TEntry : notnull, IRaftLogEntry
                where TList : notnull, IReadOnlyList<TEntry>
            {
                if (snapshotIndex.HasValue)
                {
                    logger.InstallingSnapshot(currentIndex = snapshotIndex.GetValueOrDefault());
                    replicationAwaiter = member.InstallSnapshotAsync(term, entries[0], currentIndex, token).ConfigureAwait(false).GetAwaiter();
                }
                else
                {
                    logger.ReplicaSize(member.EndPoint, entries.Count, precedingIndex, precedingTerm);
                    replicationAwaiter = member.AppendEntriesAsync<TEntry, TList>(term, entries, precedingIndex, precedingTerm, commitIndex, token).ConfigureAwait(false).GetAwaiter();
                }

                replicatedWithCurrentTerm = ContainsTerm(entries, term);
                if (replicationAwaiter.IsCompleted)
                    Complete();
                else
                    replicationAwaiter.OnCompleted(Complete);

                return new ValueTask<Result<bool>>(Task);

                static bool ContainsTerm(TList list, long term)
                {
                    for (var i = 0; i < list.Count; i++)
                    {
                        if (list[i].Term == term)
                            return true;
                    }

                    return false;
                }
            }
        }

        private sealed class WaitNode : TaskCompletionSource<bool>
        {
            public WaitNode()
                : base(TaskCreationOptions.RunContinuationsAsynchronously)
            {
            }
        }

        private readonly long currentTerm;
        private readonly bool allowPartitioning;
        private readonly CancellationTokenSource timerCancellation;
        private volatile WaitNode replicationEvent, replicationQueue;
        private Task? heartbeatTask;
        internal ILeaderStateMetrics? Metrics;

        internal LeaderState(IRaftStateMachine stateMachine, bool allowPartitioning, long term)
            : base(stateMachine)
        {
            currentTerm = term;
            this.allowPartitioning = allowPartitioning;
            timerCancellation = new CancellationTokenSource();
            replicationEvent = new WaitNode();
            replicationQueue = new WaitNode();
        }

        private async Task<bool> DoHeartbeats(IAuditTrail<IRaftLogEntry> auditTrail, CancellationToken token)
        {
            var timeStamp = Timestamp.Current;
            var tasks = new LinkedList<ValueTask<Result<bool>>>();

            long commitIndex = auditTrail.GetLastIndex(true), currentIndex = auditTrail.GetLastIndex(false);
            var term = currentTerm;

            // send heartbeat in parallel
            foreach (var member in stateMachine.Members)
            {
                if (member.IsRemote)
                {
                    long precedingIndex = Math.Max(0, member.NextIndex - 1), precedingTerm = await auditTrail.GetTermAsync(precedingIndex, token).ConfigureAwait(false);
                    tasks.AddLast(new Replicator(member, commitIndex, currentIndex, term, precedingIndex, precedingTerm, stateMachine.Logger, token).Start(auditTrail));
                }
            }

            var quorum = 1;  // because we know that the entry is replicated in this node
            var commitQuorum = 1;
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

        private void DrainReplicationQueue()
            => Interlocked.Exchange(ref replicationQueue, new WaitNode()).SetResult(true);

        private Task WaitForReplicationAsync(TimeSpan period, CancellationToken token)
        {
            // This implementation optimized to avoid allocations of a new wait node.
            // The new node should be created when the current node is in signaled state.
            // Otherwise, we can keep the existing node
            var current = replicationEvent.Task;
            if (current.IsCompleted)
            {
                replicationEvent = new WaitNode();
            }
            else
            {
                current = current.WaitAsync(period, token);
            }

            return current;
        }

        private async Task DoHeartbeats(TimeSpan period, IAuditTrail<IRaftLogEntry> auditTrail, CancellationToken token)
        {
            using var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(token, timerCancellation.Token);
            for (token = cancellationSource.Token; await DoHeartbeats(auditTrail, token).ConfigureAwait(false); await WaitForReplicationAsync(period, token).ConfigureAwait(false))
                DrainReplicationQueue();
        }

        internal Task<bool> ForceReplicationAsync(TimeSpan timeout, CancellationToken token)
        {
            // enqueue a new task representing completion callback
            var result = replicationQueue.Task.WaitAsync(timeout, token);

            // resume heartbeat loop to force replication
            replicationEvent.TrySetResult(true);
            return result;
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

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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
            private static readonly ValueFunc<long, long> IndexDecrement = new ValueFunc<long, long>(DecrementIndex);

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

            private static long DecrementIndex(long index) => index > 0L ? index - 1L : index;

            internal ValueTask<Result<bool>> Start(IAuditTrail<IRaftLogEntry> auditTrail)
            {
                logger.ReplicationStarted(member.Endpoint, currentIndex);
                return currentIndex >= member.NextIndex ?
                    auditTrail.ReadAsync<Replicator, Result<bool>>(this, member.NextIndex, token) :
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
                        logger.ReplicationSuccessful(member.Endpoint, member.NextIndex);
                        member.NextIndex.VolatileWrite(currentIndex + 1);
                        result = result.SetValue(replicatedWithCurrentTerm);
                    }
                    else
                    {
                        logger.ReplicationFailed(member.Endpoint, member.NextIndex.UpdateAndGet(in IndexDecrement));
                    }

                    SetResult(result);
                }
                catch (Exception e)
                {
                    SetException(e);
                }
            }

            private static bool ContainsTerm<TEntry, TList>(TList list, long term)
                where TEntry : IRaftLogEntry
                where TList : IReadOnlyList<TEntry>
            {
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i].Term == term)
                        return true;
                }

                return false;
            }

            public ValueTask<Result<bool>> ReadAsync<TEntry, TList>(TList entries, long? snapshotIndex, CancellationToken token)
                where TEntry : IRaftLogEntry
                where TList : IReadOnlyList<TEntry>
            {
                if (snapshotIndex.HasValue)
                {
                    logger.InstallingSnapshot(currentIndex = snapshotIndex.GetValueOrDefault());
                    replicationAwaiter = member.InstallSnapshotAsync(term, entries[0], currentIndex, token).ConfigureAwait(false).GetAwaiter();
                }
                else
                {
                    logger.ReplicaSize(member.Endpoint, entries.Count, precedingIndex, precedingTerm);
                    replicationAwaiter = member.AppendEntriesAsync<TEntry, TList>(term, entries, precedingIndex, precedingTerm, commitIndex, token).ConfigureAwait(false).GetAwaiter();
                }

                replicatedWithCurrentTerm = ContainsTerm<TEntry, TList>(entries, term);
                if (replicationAwaiter.IsCompleted)
                    Complete();
                else
                    replicationAwaiter.OnCompleted(Complete);
                return new ValueTask<Result<bool>>(Task);
            }
        }

        private sealed class WaitNode : TaskCompletionSource<bool>
        {
            internal WaitNode()
                : base(TaskCreationOptions.RunContinuationsAsynchronously)
            {
            }
        }

        private readonly long currentTerm;
        private readonly bool allowPartitioning;
        private readonly CancellationTokenSource timerCancellation;
        private readonly AsyncManualResetEvent forcedReplication;
        private Task? heartbeatTask;
        private ImmutableQueue<WaitNode> replicationQueue;  // volatile
        internal ILeaderStateMetrics? Metrics;

        internal LeaderState(IRaftStateMachine stateMachine, bool allowPartitioning, long term)
            : base(stateMachine)
        {
            currentTerm = term;
            this.allowPartitioning = allowPartitioning;
            timerCancellation = new CancellationTokenSource();
            forcedReplication = new AsyncManualResetEvent(false);
            replicationQueue = ImmutableQueue<WaitNode>.Empty;
        }

        private bool CheckTerm(long term)
        {
            if (term <= currentTerm)
                return true;
            stateMachine.MoveToFollowerState(false, term);
            return false;
        }

        [SuppressMessage("Reliability", "CA2012", Justification = "Replicator task should be added to collection to ensure parallelism")]
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
            for (var task = tasks.First; task != null; task.Value = default, task = task.Next)
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

            // majority of nodes accept entries with a least one entry from current term
            if (commitQuorum > 0)
            {
                var count = await auditTrail.CommitAsync(currentIndex, token).ConfigureAwait(false); // commit all entries started from first uncommitted index to the end
                stateMachine.Logger.CommitSuccessful(commitIndex + 1, count);
                return CheckTerm(term);
            }

            stateMachine.Logger.CommitFailed(quorum, commitIndex);

            // majority of nodes replicated, continue leading if current term is not changed
            if (quorum > 0 || allowPartitioning)
                return CheckTerm(term);

            // it is partitioned network with absolute majority, not possible to have more than one leader
            stateMachine.MoveToFollowerState(false, term);
            return false;
        }

        private void DrainReplicationQueue()
        {
            foreach (var waiter in Interlocked.Exchange(ref replicationQueue, ImmutableQueue<WaitNode>.Empty))
                waiter.SetResult(true);
        }

        private async Task DoHeartbeats(TimeSpan period, IAuditTrail<IRaftLogEntry> auditTrail, CancellationToken token)
        {
            using var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(token, timerCancellation.Token);
            for (token = cancellationSource.Token; await DoHeartbeats(auditTrail, token).ConfigureAwait(false); await forcedReplication.WaitAsync(period, token).ConfigureAwait(false))
                DrainReplicationQueue();
        }

        internal Task<bool> ForceReplicationAsync(TimeSpan timeout, CancellationToken token)
        {
            var waiter = new WaitNode();
            ImmutableInterlocked.Enqueue(ref replicationQueue, waiter);
            forcedReplication.Set(true);
            return waiter.Task.WaitAsync(timeout, token);
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
                forcedReplication.Dispose();
                heartbeatTask = null;

                // cancel queue
                foreach (var waiter in Interlocked.Exchange(ref replicationQueue, ImmutableQueue<WaitNode>.Empty))
                    waiter.SetCanceled();
            }

            base.Dispose(disposing);
        }
    }
}

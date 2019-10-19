using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Replication;
    using Threading;
    using static Threading.Tasks.Continuation;
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
            //state
            private long currentIndex;
            private bool replicatedWithCurrentTerm;
            private ConfiguredTaskAwaitable<Result<bool>>.ConfiguredTaskAwaiter replicationAwaiter;

            internal Replicator(IRaftClusterMember member,
                long commitIndex,
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
                this.term = term;
                this.logger = logger;
                this.token = token;
            }

            private static long DecrementIndex(long index) => index > 0L ? index - 1L : index;

            internal ValueTask<Result<bool>> Start(IAuditTrail<IRaftLogEntry> auditTrail)
            {
                var currentIndex = this.currentIndex = auditTrail.GetLastIndex(false);
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
                    //analyze result and decrease node index when it is out-of-sync with the current node
                    if (result.Value)
                    {
                        logger.ReplicationSuccessful(member.Endpoint, member.NextIndex);
                        member.NextIndex.VolatileWrite(currentIndex + 1);
                        result = result.SetValue(replicatedWithCurrentTerm);
                    }
                    else
                        logger.ReplicationFailed(member.Endpoint, member.NextIndex.UpdateAndGet(in IndexDecrement));
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
                    if (list[i].Term == term)
                        return true;
                return false;
            }

            public ValueTask<Result<bool>> ReadAsync<TEntry, TList>(TList entries, long? snapshotIndex, CancellationToken token)
                where TEntry : IRaftLogEntry
                where TList : IReadOnlyList<TEntry>
            {
                if (snapshotIndex.HasValue)
                {
                    logger.InstallingSnapshot(currentIndex = snapshotIndex.Value);
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


        private Task heartbeatTask;
        private readonly long currentTerm;
        private readonly bool allowPartitioning;
        private readonly CancellationTokenSource timerCancellation;
        private readonly AsyncManualResetEvent forcedReplication;
        internal ILeaderStateMetrics Metrics;

        internal LeaderState(IRaftStateMachine stateMachine, bool allowPartitioning, long term)
            : base(stateMachine)
        {
            currentTerm = term;
            this.allowPartitioning = allowPartitioning;
            timerCancellation = new CancellationTokenSource();
            forcedReplication = new AsyncManualResetEvent(false);
        }

        private bool CheckTerm(long term)
        {
            if (term <= currentTerm)
                return true;
            stateMachine.MoveToFollowerState(false, term);
            return false;
        }

        private async Task<bool> DoHeartbeats(IAuditTrail<IRaftLogEntry> auditTrail, CancellationToken token)
        {
            var timeStamp = Timestamp.Current;
            var tasks = new LinkedList<ValueTask<Result<bool>>>();
            //send heartbeat in parallel
            var commitIndex = auditTrail.GetLastIndex(true);
            var term = currentTerm;
            foreach (var member in stateMachine.Members)
                if (member.IsRemote)
                {
                    long precedingIndex = Math.Max(0, member.NextIndex - 1), precedingTerm = await auditTrail.GetTermAsync(precedingIndex, token);
                    tasks.AddLast(new Replicator(member, commitIndex, term, precedingIndex, precedingTerm, stateMachine.Logger, token).Start(auditTrail));
                }
            var quorum = 1;  //because we know that the entry is replicated in this node
            var commitQuorum = 1;
            for (var task = tasks.First; task != null; task.Value = default, task = task.Next)
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
                catch (OperationCanceledException)//leading was canceled
                {
                    tasks.Clear();
                    Metrics?.ReportBroadcastTime(timeStamp.Elapsed);
                    return false;
                }
                catch (Exception e)
                {
                    stateMachine.Logger.LogError(e, ExceptionMessages.UnexpectedError);
                }

            tasks.Clear();
            Metrics?.ReportBroadcastTime(timeStamp.Elapsed);
            //majority of nodes accept entries with a least one entry from current term
            if (commitQuorum > 0)
            {
                var count = await auditTrail.CommitAsync(token).ConfigureAwait(false); //commit all entries started from first uncommitted index to the end
                stateMachine.Logger.CommitSuccessful(commitIndex + 1, count);
                return CheckTerm(term);
            }
            stateMachine.Logger.CommitFailed(quorum, commitIndex);
            //majority of nodes replicated, continue leading if current term is not changed
            if (quorum > 0 || allowPartitioning)
                return CheckTerm(term);
            //it is partitioned network with absolute majority, not possible to have more than one leader
            stateMachine.MoveToFollowerState(false, term);
            return false;
        }

        private async Task DoHeartbeats(TimeSpan period, IAuditTrail<IRaftLogEntry> auditTrail)
        {
            var token = timerCancellation.Token;
            while (await DoHeartbeats(auditTrail, token).ConfigureAwait(false))
                await forcedReplication.Wait(period, token).ConfigureAwait(false);
        }

        internal void ForceReplication() => forcedReplication.Set(true);

        /// <summary>
        /// Starts cluster synchronization.
        /// </summary>
        /// <param name="period">Time period of Heartbeats</param>
        /// <param name="transactionLog">Transaction log.</param>
        internal LeaderState StartLeading(TimeSpan period, IAuditTrail<IRaftLogEntry> transactionLog)
        {
            foreach (var member in stateMachine.Members)
                member.NextIndex = transactionLog.GetLastIndex(false) + 1;
            heartbeatTask = DoHeartbeats(period, transactionLog);
            return this;
        }

        //the token that can be used to track leadership
        internal CancellationToken Token => IsDisposed ? new CancellationToken(true) : timerCancellation.Token;

        internal override Task StopAsync()
        {
            timerCancellation.Cancel(false);
            return heartbeatTask.OnCompleted();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timerCancellation.Dispose();
                forcedReplication.Dispose();
                heartbeatTask = null;
            }
            base.Dispose(disposing);
        }
    }
}

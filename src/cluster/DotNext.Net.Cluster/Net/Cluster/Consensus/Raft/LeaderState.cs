using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private enum MemberHealthStatus
        {
            Unavailable = 0,
            Replicated, //means that node accepts the entries but not for the current term
            ReplicatedWithCurrentTerm,  //means that node accepts the entries with the current term
            Canceled
        }

        private readonly struct Replicator : ILogEntryReader<IRaftLogEntry, Result<bool>>
        {
            private readonly IRaftClusterMember member;
            private readonly long commitIndex;
            private readonly long term;
            private readonly long precedingIndex;
            private readonly long precedingTerm;
            private readonly long currentIndex;
            private readonly ILogger logger;

            internal Replicator(IRaftClusterMember member,
                long currentIndex,
                long commitIndex,
                long term,
                long precedingIndex,
                long precedingTerm,
                ILogger logger)
            {
                this.currentIndex = currentIndex;
                this.member = member;
                this.term = term;
                this.precedingIndex = precedingIndex;
                this.precedingTerm = precedingTerm;
                this.commitIndex = commitIndex;
                this.logger = logger;
            }

            async ValueTask<Result<bool>> ILogEntryReader<IRaftLogEntry, Result<bool>>.ReadAsync<TEntryImpl, TList>(TList entries, long? snapshotIndex, CancellationToken token)
            {
                logger.ReplicationStarted(member.Endpoint, currentIndex);
                Result<bool> result;
                if (snapshotIndex.HasValue)
                {
                    logger.InstallingSnapshot(snapshotIndex.Value);
                    //install snapshot
                    result = await member.InstallSnapshotAsync(term, entries[0], snapshotIndex.Value, token).ConfigureAwait(false);
                }
                else
                {
                    logger.ReplicaSize(member.Endpoint, entries.Count, precedingIndex, precedingTerm);
                    //trying to replicate
                    result = await member.AppendEntriesAsync(term, entries, precedingIndex, precedingTerm, commitIndex, token).ConfigureAwait(false);
                }
                //analyze result and decrease node index when it is out-of-sync with the current node
                if (result.Value)
                {
                    logger.ReplicationSuccessful(member.Endpoint, member.NextIndex);
                    member.NextIndex.VolatileWrite(currentIndex + 1);
                    //checks whether the at least one entry from current term is stored on this node
                    result = result.SetValue(entries.ContainsTerm<TEntryImpl, TList>(term));
                }
                else
                    logger.ReplicationFailed(member.Endpoint, member.NextIndex.UpdateAndGet(in IndexDecrement));
                return result;
            }
        }

        private static readonly ValueFunc<long, long> IndexDecrement = new ValueFunc<long, long>(DecrementIndex);
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

        private static long DecrementIndex(long index) => index > 0L ? index - 1L : index;

        private static Result<MemberHealthStatus> HealthStatusContinuation(Task<Result<bool>> task)
        {
            Result<MemberHealthStatus> result;
            if (task.IsCanceled)
                result = new Result<MemberHealthStatus>(long.MinValue, MemberHealthStatus.Canceled);
            else if (task.IsFaulted)
                result = new Result<MemberHealthStatus>(long.MinValue, MemberHealthStatus.Unavailable);
            else
                result = new Result<MemberHealthStatus>(task.Result.Term, task.Result.Value ? MemberHealthStatus.ReplicatedWithCurrentTerm : MemberHealthStatus.Replicated);
            return result;
        }

        private bool CheckTerm(long term)
        {
            if (term <= currentTerm)
                return true;
            stateMachine.MoveToFollowerState(false, term);
            return false;
        }

        private async Task<bool> DoHeartbeats(IAuditTrail<IRaftLogEntry> transactionLog)
        {
            var timeStamp = Timestamp.Current;
            ICollection<Task<Result<bool>>> tasks = new LinkedList<Task<Result<bool>>>();
            //send heartbeat in parallel
            var commitIndex = transactionLog.GetLastIndex(true);
            var term = currentTerm;
            foreach (var member in stateMachine.Members)
                if (member.IsRemote)
                {
                    long currentIndex = transactionLog.GetLastIndex(false),
                        precedingIndex = Math.Max(0, member.NextIndex - 1),
                        precedingTerm = await transactionLog.GetTermAsync(precedingIndex, timerCancellation.Token).ConfigureAwait(false);
                    tasks.Add(transactionLog.ReadEntriesAsync<Replicator, Result<bool>>(new Replicator(member, currentIndex, commitIndex, term, precedingIndex, precedingTerm, stateMachine.Logger), member.NextIndex, timerCancellation.Token).AsTask());
                }
            var quorum = 1;  //because we know that the entry is replicated in this node
            var commitQuorum = 1;
            foreach (var task in tasks)
                try
                {
                    var result = await task.ConfigureAwait(false);
                    term = Math.Max(term, result.Term);
                    quorum += 1;
                    commitQuorum += result.Value ? 1 : -1;
                }
                catch (MemberUnavailableException e)
                {
                    quorum -= 1;
                    commitQuorum -= 1;
                }
                catch (OperationCanceledException e)
                {
                    //leading was canceled
                    //ensure that all requests are canceled
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                    Metrics?.ReportBroadcastTime(timeStamp.Elapsed);
                    return false;
                }
                finally
                {
                    task.Dispose();
                }

            Metrics?.ReportBroadcastTime(timeStamp.Elapsed);
            tasks.Clear();
            //majority of nodes accept entries with a least one entry from current term
            if (commitQuorum > 0)
            {
                var count = await transactionLog.CommitAsync(timerCancellation.Token).ConfigureAwait(false); //commit all entries started from first uncommitted index to the end
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
            while (await DoHeartbeats(auditTrail).ConfigureAwait(false))
                await forcedReplication.Wait(period, timerCancellation.Token).ConfigureAwait(false);
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

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

        //true if at least one entry from current term is stored on this node; otherwise, false
        private static async Task<Result<bool>> AppendEntriesAsync(IRaftClusterMember member, long commitIndex,
            long term,
            IAuditTrail<IRaftLogEntry> transactionLog, ILogger logger, CancellationToken token)
        {
            var currentIndex = transactionLog.GetLastIndex(false);
            logger.ReplicationStarted(member.Endpoint, currentIndex);
            var precedingIndex = Math.Max(0, member.NextIndex - 1);
            var precedingTerm = await transactionLog.GetTermAsync(precedingIndex, token).ConfigureAwait(false);
            Result<bool> result;
            using (var entries = currentIndex >= member.NextIndex ? await transactionLog.GetEntriesAsync(member.NextIndex, token).ConfigureAwait(false) : new LogEntryList<IRaftLogEntry>())
            {
                if (entries.SnapshotIndex is long snapshotIndex)
                {
                    logger.InstallingSnapshot(snapshotIndex);
                    //install snapshot
                    result = await member.InstallSnapshotAsync(term, entries[0], snapshotIndex, token).ConfigureAwait(false);
                    currentIndex = snapshotIndex;
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
                    result = result.SetValue(entries.Any(entry => entry.Term == term));
                }
                else
                    logger.ReplicationFailed(member.Endpoint, member.NextIndex.UpdateAndGet(in IndexDecrement));
            }
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
            ICollection<Task<Result<MemberHealthStatus>>> tasks = new LinkedList<Task<Result<MemberHealthStatus>>>();
            //send heartbeat in parallel
            var commitIndex = transactionLog.GetLastIndex(true);
            Func<Task<Result<bool>>, Result<MemberHealthStatus>> continuation = HealthStatusContinuation;
            foreach (var member in stateMachine.Members)
                if (member.IsRemote)
                    tasks.Add(AppendEntriesAsync(member, commitIndex, currentTerm, transactionLog, stateMachine.Logger, timerCancellation.Token)
                        .ContinueWith(continuation, default, TaskContinuationOptions.ExecuteSynchronously,
                            TaskScheduler.Current));
            var quorum = 1;  //because we know that the entry is replicated in this node
            var commitQuorum = 1;
            var term = currentTerm;
            foreach (var task in tasks)
            {
                var result = await task.ConfigureAwait(false);
                term = Math.Max(term, result.Term);
                switch (result.Value)
                {
                    case MemberHealthStatus.Canceled: //leading was canceled
                        //ensure that all requests are canceled
                        await Task.WhenAll(tasks).ConfigureAwait(false);
                        Metrics?.ReportBroadcastTime(timeStamp.Elapsed);
                        return false;
                    case MemberHealthStatus.Replicated:
                        quorum += 1;
                        commitQuorum -= 1;
                        break;
                    case MemberHealthStatus.ReplicatedWithCurrentTerm:
                        quorum += 1;
                        commitQuorum += 1;
                        break;
                    case MemberHealthStatus.Unavailable:
                        quorum -= 1;
                        commitQuorum -= 1;
                        break;
                }

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

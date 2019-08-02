using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Replication;
    using Threading;

    internal sealed class LeaderState : RaftState
    {
        private enum MemberHealthStatus
        {
            Unavailable = 0,
            Replicated, //means that node accepts the entries but not for the current term
            ReplicatedWithCurrentTerm,  //means that node accepts the entries with the current term
            Canceled
        }

        private static readonly Func<Task<Result<bool>>, Result<MemberHealthStatus>> HealthStatusContinuation = task =>
        {
            Result<MemberHealthStatus> result;
            if (task.IsCanceled)
                result = new Result<MemberHealthStatus>(long.MinValue, MemberHealthStatus.Canceled);
            else if (task.IsFaulted)
                result = new Result<MemberHealthStatus>(long.MinValue, MemberHealthStatus.Unavailable);
            else
                result = new Result<MemberHealthStatus>(task.Result.Term, task.Result.Value ? MemberHealthStatus.ReplicatedWithCurrentTerm : MemberHealthStatus.Replicated);
            return result;
        };

        private long heartbeatCounter;
        private volatile RegisteredWaitHandle heartbeatTimer;
        private readonly long currentTerm;
        private readonly bool allowPartitioning;
        private readonly CancellationTokenSource timerCancellation;

        internal LeaderState(IRaftStateMachine stateMachine, bool allowPartitioning, long term)
            : base(stateMachine)
        {
            currentTerm = term;
            this.allowPartitioning = allowPartitioning;
            timerCancellation = new CancellationTokenSource();
        }

        //true if at least one entry from current term is stored on this node; otherwise, false
        private static async Task<Result<bool>> AppendEntriesAsync(IRaftClusterMember member, long commitIndex,
            long term,
            IAuditTrail<ILogEntry> transactionLog, ILogger logger, CancellationToken token)
        {
            var currentIndex = transactionLog.GetLastIndex(false);
            logger.ReplicationStarted(member.Endpoint, currentIndex);
            var precedingIndex = Math.Max(0, member.NextIndex - 1);
            var precedingTerm = (await transactionLog.GetEntryAsync(precedingIndex).ConfigureAwait(false) ??
                                 transactionLog.First).Term;
            var entries = currentIndex >= member.NextIndex
                ? await transactionLog.GetEntriesAsync(member.NextIndex).ConfigureAwait(false)
                : Array.Empty<ILogEntry>();
            logger.ReplicaSize(member.Endpoint, entries.Count, precedingIndex, precedingTerm);
            //trying to replicate
            var result = await member
                .AppendEntriesAsync(term, entries, precedingIndex, precedingTerm, commitIndex, token)
                .ConfigureAwait(false);
            if (result.Value)
            {
                logger.ReplicationSuccessful(member.Endpoint, member.NextIndex);
                member.NextIndex.VolatileWrite(currentIndex + 1);
                //checks whether the at least one entry from current term is stored on this node
                result = result.SetValue(entries.Any(entry => entry.Term == term));
            }
            else
                logger.ReplicationFailed(member.Endpoint, member.NextIndex.DecrementAndGet());

            return result;
        }

        private bool CheckTerm(long term)
        {
            if (term <= currentTerm)
                return true;
            stateMachine.MoveToFollowerState(false, term);
            return false;
        }

        private async Task<bool> DoHeartbeats(IAuditTrail<ILogEntry> transactionLog)
        {
            ICollection<Task<Result<MemberHealthStatus>>> tasks = new LinkedList<Task<Result<MemberHealthStatus>>>();
            //send heartbeat in parallel
            var commitIndex = transactionLog.GetLastIndex(true);
            foreach (var member in stateMachine.Members)
                if (member.IsRemote)
                    tasks.Add(AppendEntriesAsync(member, commitIndex, currentTerm, transactionLog, stateMachine.Logger, timerCancellation.Token)
                        .ContinueWith(HealthStatusContinuation, default, TaskContinuationOptions.ExecuteSynchronously,
                            TaskScheduler.Current));
            var quorum = 1;  //because we know the entry is replicated in this node
            var commitQuorum = 1;
            var term = currentTerm;
            foreach (var task in tasks)
            {
                var result = await task.ConfigureAwait(false);
                term = Math.Max(term, result.Term);
                switch (result.Value)
                {
                    case MemberHealthStatus.Canceled: //leading was canceled
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

            tasks.Clear();
            //majority of nodes accept entries with a least one entry from current term
            if (commitQuorum > 0)
            {
                commitIndex += 1;
                var count = await transactionLog.CommitAsync(commitIndex); //commit all entries started from first uncommitted index to the end
                stateMachine.Logger.CommitSuccessful(commitIndex, count);
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

        private async void DoHeartbeats(object transactionLog, bool timedOut)
        {
            Debug.Assert(transactionLog is IAuditTrail<ILogEntry>);
            if (IsDisposed || !timedOut || heartbeatCounter.IncrementAndGet() > 1L)
                return;
            do
            {
                if (await DoHeartbeats((IAuditTrail<ILogEntry>)transactionLog).ConfigureAwait(false))
                    continue;
                heartbeatTimer?.Unregister(null);
                break;
            }
            while (heartbeatCounter.DecrementAndGet() > 0L);
        }

        internal void ForceReplication(IAuditTrail<ILogEntry> transactionLog)
            => DoHeartbeats(transactionLog, true);

        /// <summary>
        /// Starts cluster synchronization.
        /// </summary>
        /// <param name="period">Time period of Heartbeats</param>
        /// <param name="transactionLog">Transaction log.</param>
        internal LeaderState StartLeading(TimeSpan period, IAuditTrail<ILogEntry> transactionLog)
        {
            heartbeatCounter.VolatileWrite(0L);
            if (transactionLog != null)
                foreach (var member in stateMachine.Members)
                    member.NextIndex = transactionLog.GetLastIndex(false) + 1;
            heartbeatTimer = ThreadPool.RegisterWaitForSingleObject(timerCancellation.Token.WaitHandle, DoHeartbeats,
                transactionLog, period, false);
            DoHeartbeats(transactionLog, true); //execute heartbeats immediately without delay provided by RegisterWaitForSingleObject
            return this;
        }

        private static async Task StopLeading(RegisteredWaitHandle handle)
        {
            using (var signal = new ManualResetEvent(false))
            {
                handle.Unregister(signal);
                await signal.WaitAsync();
            }
        }

        internal Task StopLeading()
        {
            var handle = Interlocked.Exchange(ref heartbeatTimer, null);
            if (handle is null || timerCancellation.IsCancellationRequested)
                return Task.CompletedTask;
            timerCancellation.Cancel(false);
            return StopLeading(handle);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timerCancellation.Dispose();
                Interlocked.Exchange(ref heartbeatTimer, null)?.Unregister(null);
            }
            base.Dispose(disposing);
        }
    }
}

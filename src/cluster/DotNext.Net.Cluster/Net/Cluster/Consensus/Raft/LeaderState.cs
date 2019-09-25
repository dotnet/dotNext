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
        private enum ReplicationStatus
        {
            Unavailable = 0,
            Replicated, //means that node accepts the entries but not for the current term
            ReplicatedWithCurrentTerm,  //means that node accepts the entries with the current term
            Canceled
        }

        private sealed class Replicator : TaskCompletionSource<Result<ReplicationStatus>>, ILogEntryConsumer<IRaftLogEntry, Result<ReplicationStatus>>
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

            internal Task<Result<ReplicationStatus>> Start(IAuditTrail<IRaftLogEntry> transactionLog)
            {
                logger.ReplicationStarted(member.Endpoint, currentIndex = transactionLog.GetLastIndex(false));
                return transactionLog.ReadEntriesAsync<Replicator, Result<ReplicationStatus>>(this, member.NextIndex, token).AsTask();
            }

            private void Complete()
            {
                try
                {
                    var result = replicationAwaiter.GetResult();
                    replicationAwaiter = default;
                    var status = ReplicationStatus.Replicated;
                    //analyze result and decrease node index when it is out-of-sync with the current node
                    if (result.Value)
                    {
                        logger.ReplicationSuccessful(member.Endpoint, member.NextIndex);
                        member.NextIndex.VolatileWrite(currentIndex + 1);
                        status += replicatedWithCurrentTerm.ToInt32();
                    }
                    else
                        logger.ReplicationFailed(member.Endpoint, member.NextIndex.UpdateAndGet(in IndexDecrement));
                    SetResult(result.SetValue(status));
                }
                catch (OperationCanceledException)
                {
                    SetResult(new Result<ReplicationStatus>(term, ReplicationStatus.Canceled));
                }
                catch (MemberUnavailableException)
                {
                    SetResult(new Result<ReplicationStatus>(term, ReplicationStatus.Unavailable));
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

            ValueTask<Result<ReplicationStatus>> ILogEntryConsumer<IRaftLogEntry, Result<ReplicationStatus>>.ReadAsync<TEntryImpl, TList>(TList entries, long? snapshotIndex, CancellationToken token)
            {
                if (snapshotIndex.HasValue)
                {
                    logger.InstallingSnapshot(currentIndex = snapshotIndex.Value);
                    replicationAwaiter = member.InstallSnapshotAsync(term, entries[0], currentIndex, token).ConfigureAwait(false).GetAwaiter();
                }
                else
                {
                    logger.ReplicaSize(member.Endpoint, entries.Count, precedingIndex, precedingTerm);
                    replicationAwaiter = member.AppendEntriesAsync<TEntryImpl, TList>(term, entries, precedingIndex, precedingTerm, commitIndex, token).ConfigureAwait(false).GetAwaiter();
                }
                replicatedWithCurrentTerm = ContainsTerm<TEntryImpl, TList>(entries, term);
                if(replicationAwaiter.IsCompleted)
                    Complete();
                else
                    replicationAwaiter.OnCompleted(Complete);
                return new ValueTask<Result<ReplicationStatus>>(Task);
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

        private async Task<bool> DoHeartbeats(IAuditTrail<IRaftLogEntry> transactionLog, CancellationToken token)
        {
            var timeStamp = Timestamp.Current;
            ICollection<Task<Result<ReplicationStatus>>> tasks = new LinkedList<Task<Result<ReplicationStatus>>>();
            //send heartbeat in parallel
            var commitIndex = transactionLog.GetLastIndex(true);
            var term = currentTerm;
            foreach (var member in stateMachine.Members)
                if (member.IsRemote)
                {
                    long precedingIndex = Math.Max(0, member.NextIndex - 1), precedingTerm = await transactionLog.GetTermAsync(precedingIndex, token);
                    tasks.Add(new Replicator(member, commitIndex, term, precedingIndex, precedingTerm, stateMachine.Logger, token).Start(transactionLog));                
                }
            var quorum = 1;  //because we know that the entry is replicated in this node
            var commitQuorum = 1;
            foreach (var task in tasks)
            {
                var result = await task.ConfigureAwait(false);
                term = Math.Max(term, result.Term);
                switch (result.Value)
                {
                    case ReplicationStatus.Canceled: //leading was canceled
                        //ensure that all requests are canceled
                        await Task.WhenAll(tasks).ConfigureAwait(false);
                        Metrics?.ReportBroadcastTime(timeStamp.Elapsed);
                        return false;
                    case ReplicationStatus.Replicated:
                        quorum += 1;
                        commitQuorum -= 1;
                        break;
                    case ReplicationStatus.ReplicatedWithCurrentTerm:
                        quorum += 1;
                        commitQuorum += 1;
                        break;
                    case ReplicationStatus.Unavailable:
                        quorum -= 1;
                        commitQuorum -= 1;
                        break;
                }
            }

            Metrics?.ReportBroadcastTime(timeStamp.Elapsed);
            tasks.Clear();
            //majority of nodes accept entries with a least one entry from current term
            if (commitQuorum > 0)
            {
                var count = await transactionLog.CommitAsync(token).ConfigureAwait(false); //commit all entries started from first uncommitted index to the end
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

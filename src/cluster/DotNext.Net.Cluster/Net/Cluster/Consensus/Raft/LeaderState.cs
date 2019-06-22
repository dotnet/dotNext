using DotNext.Net.Cluster.Replication;
using DotNext.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal sealed class LeaderState : RaftState
    {
        private enum MemberHealthStatus
        {
            OK,
            Canceled,
            Unavailable
        }

        private static readonly Func<Task, MemberHealthStatus> HealthStatusContinuation = task =>
        {
            if (task.IsCanceled)
                return MemberHealthStatus.Canceled;
            else if (task.IsFaulted)
                return MemberHealthStatus.Unavailable;
            else
                return MemberHealthStatus.OK;
        };

        private BackgroundTask heartbeatTask;
        private readonly long term;
        private readonly bool absoluteMajority;

        internal LeaderState(IRaftStateMachine stateMachine, bool absoluteMajority, long term) 
            : base(stateMachine)
        {
            this.term = term;
            this.absoluteMajority = absoluteMajority;
        }

        private async Task DoHeartbeats(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                ICollection<Task<MemberHealthStatus>> tasks = new LinkedList<Task<MemberHealthStatus>>();
                //send heartbeat in parallel
                foreach (var member in stateMachine.Members)
                {
                    stateMachine.Logger.SendingHearbeat(member.Endpoint);
                    tasks.Add(member.HeartbeatAsync(term, token).ContinueWith(HealthStatusContinuation, default,
                        TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current));
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
                var votes = 0;
                if (absoluteMajority)
                    foreach (var task in tasks)
                        switch (task.Result)
                        {
                            case MemberHealthStatus.Canceled:
                                return;
                            case MemberHealthStatus.OK:
                                votes += 1;
                                break;
                            case MemberHealthStatus.Unavailable:
                                votes -= 1;
                                break;
                        }
                else
                    votes = int.MaxValue;
                tasks.Clear();
                if (votes <= 0)
                {
                    stateMachine.MoveToFollowerState(false);
                    return;
                }
            }
        }

        private static async Task AppendEntriesAsync(IRaftClusterMember member, long term, ILogEntry<LogEntryId> newEntry,
            LogEntryId lastEntry, IAuditTrail<LogEntryId> transactionLog, ILogger logger, CancellationToken token)
        {
            logger.ReplicationStarted(member.Endpoint, newEntry.Id);
            if (await member.AppendEntriesAsync(term, newEntry, lastEntry, token).ConfigureAwait(false))
            {
                logger.ReplicationCompleted(member.Endpoint, newEntry.Id);
                return;
            }

            //unable to commit fresh entry, tries to commit older entries
            var lookup = lastEntry == transactionLog.Initial
                ? throw new ReplicationException(member)
                : transactionLog[lastEntry];
            while (!await member.AppendEntriesAsync(term, lookup, transactionLog.GetPrevious(lookup.Id), token)
                .ConfigureAwait(false))
            {
                lookup = transactionLog.GetPrevious(lookup);
                if (lookup is null || lookup.Id == transactionLog.Initial)
                    throw new ReplicationException(member);
            }

            //now lookup is committed, try to commit all new entries
            lookup = transactionLog.GetNext(lookup);
            while (lookup != null)
                if (await member.AppendEntriesAsync(term, lookup, transactionLog.GetPrevious(lookup.Id), token)
                    .ConfigureAwait(false))
                    lookup = transactionLog.GetNext(lookup);
                else
                    throw new ReplicationException(member);
            //now transaction log is restored on remote side, commit the fresh entry
            if (await member.AppendEntriesAsync(term, newEntry, lastEntry, token).ConfigureAwait(false))
                logger.ReplicationCompleted(member.Endpoint, newEntry.Id);
            else
                throw new ReplicationException(member);
        }

        internal async Task AppendEntriesAsync(ILogEntry<LogEntryId> entry, IAuditTrail<LogEntryId> transactionLog, CancellationToken token)
        {
            using (var tokenSource = heartbeatTask.CreateLinkedTokenSource(token))
            {
                var lastEntry = transactionLog.LastRecord;
                ICollection<Task> tasks = new LinkedList<Task>();
                foreach (var member in stateMachine.Members)
                    if (member.IsRemote)
                        tasks.Add(AppendEntriesAsync(member, term, entry, lastEntry, transactionLog, stateMachine.Logger, tokenSource.Token));
                await Task.WhenAll(tasks).ConfigureAwait(false);
                //now the record is accepted by other nodes, commit it locally
                await transactionLog.CommitAsync(entry).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Starts cluster synchronization.
        /// </summary>
        internal void StartLeading()
            => heartbeatTask = new BackgroundTask(DoHeartbeats);
        internal Task StopLeading() => heartbeatTask.Stop();

        internal Task StopLeading(CancellationToken token) => heartbeatTask.Stop(token);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                heartbeatTask.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

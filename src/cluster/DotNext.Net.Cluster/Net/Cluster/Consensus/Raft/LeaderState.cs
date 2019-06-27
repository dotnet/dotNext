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
            Unavailable = 0,
            Ok,
            Canceled
        }

        private static readonly Func<Task, MemberHealthStatus> HealthStatusContinuation = task =>
        {
            if (task.IsCanceled)
                return MemberHealthStatus.Canceled;
            else if (task.IsFaulted)
                return MemberHealthStatus.Unavailable;
            else
                return MemberHealthStatus.Ok;
        };

        private AtomicBoolean processingState;
        private volatile RegisteredWaitHandle heartbeatTimer;
        private readonly long term;
        private readonly bool absoluteMajority;
        private readonly CancellationTokenSource timerCancellation;

        internal LeaderState(IRaftStateMachine stateMachine, bool absoluteMajority, long term) 
            : base(stateMachine)
        {
            this.term = term;
            this.absoluteMajority = absoluteMajority;
            timerCancellation = new CancellationTokenSource();
            processingState = new AtomicBoolean(false);
        }

        private async Task<bool> DoHeartbeats()
        {
            ICollection<Task<MemberHealthStatus>> tasks = new LinkedList<Task<MemberHealthStatus>>();
            //send heartbeat in parallel
            foreach (var member in stateMachine.Members)
            {
                stateMachine.Logger.SendingHearbeat(member.Endpoint);
                tasks.Add(member.HeartbeatAsync(term, timerCancellation.Token).ContinueWith(HealthStatusContinuation, default,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
            var votes = 0;
            if (absoluteMajority)
                foreach (var task in tasks)
                    switch (task.Result)
                    {
                        case MemberHealthStatus.Canceled:
                            return false;
                        case MemberHealthStatus.Ok:
                            votes += 1;
                            break;
                        case MemberHealthStatus.Unavailable:
                            votes -= 1;
                            break;
                    }
            else
                votes = int.MaxValue;

            tasks.Clear();
            if (votes > 0) return true;
            stateMachine.MoveToFollowerState(false);
            return false;
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
            using (var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(timerCancellation.Token, token))
            {
                token = tokenSource.Token;
                var lastEntry = transactionLog.LastRecord;
                ICollection<Task> tasks = new LinkedList<Task>();
                foreach (var member in stateMachine.Members)
                    if (member.IsRemote)
                        tasks.Add(AppendEntriesAsync(member, term, entry, lastEntry, transactionLog, stateMachine.Logger, token));
                await Task.WhenAll(tasks).ConfigureAwait(false);
                //now the record is accepted by other nodes, commit it locally
                await transactionLog.CommitAsync(entry).ConfigureAwait(false);
            }
        }

        private async void DoHeartbeats(object state, bool timedOut)
        {
            if (IsDisposed || !timedOut || !processingState.FalseToTrue()) return;
            try
            {
                if (!await DoHeartbeats().ConfigureAwait(false))
                    heartbeatTimer?.Unregister(null);
            }
            finally
            {
                processingState.Value = false;
            }
        }

        /// <summary>
        /// Starts cluster synchronization.
        /// </summary>
        internal LeaderState StartLeading(int delay)
        {
            processingState.Value = false;
            heartbeatTimer = ThreadPool.RegisterWaitForSingleObject(timerCancellation.Token.WaitHandle, DoHeartbeats,
                null, delay, false);
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

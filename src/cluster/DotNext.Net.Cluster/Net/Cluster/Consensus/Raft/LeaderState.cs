using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Replication;
    using Threading;

    internal sealed class LeaderState : RaftState
    {
        private enum MemberHealthStatus
        {
            Unavailable = 0,
            Responded,
            Canceled
        }

        private static readonly Func<Task<long>, Result<MemberHealthStatus>> HealthStatusContinuation = task =>
        {
            if (task.IsCanceled)
                return new Result<MemberHealthStatus>(long.MinValue, MemberHealthStatus.Canceled);
            if (task.IsFaulted)
                return new Result<MemberHealthStatus>(long.MinValue, MemberHealthStatus.Unavailable);
            return new Result<MemberHealthStatus>(task.Result, MemberHealthStatus.Responded);
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

        private static async Task<long> AppendEntriesAsync(IRaftClusterMember member, long commitIndex, long term,
            IAuditTrail<ILogEntry> transactionLog, CancellationToken token)
        {
            var currentIndex = transactionLog.GetLastIndex(false);
            retry:
            var precedingIndex = member.NextIndex - 1;
            var precedingTerm = (await transactionLog.GetEntryAsync(precedingIndex).ConfigureAwait(false) ??
                                 transactionLog.First).Term;
            var entries = currentIndex >= member.NextIndex
                ? await transactionLog.GetEntriesAsync(member.NextIndex).ConfigureAwait(false)
                : Array.Empty<ILogEntry>();
            //trying to replicate
            var result = await member
                .AppendEntriesAsync(term, entries, precedingIndex, precedingTerm, commitIndex, token)
                .ConfigureAwait(false);
            if (result.Term > term)
                return result.Term;
            if (result.Value)
            {
                member.NextIndex.VolatileWrite(currentIndex + 1);
                return result.Term;
            }

            member.NextIndex.DecrementAndGet();
            goto retry;
        }

        private async Task<bool> DoHeartbeats(IAuditTrail<ILogEntry> transactionLog)
        {
            ICollection<Task<Result<MemberHealthStatus>>> tasks = new LinkedList<Task<Result<MemberHealthStatus>>>();
            //send heartbeat in parallel
            var commitIndex = transactionLog.GetLastIndex(true);
            foreach (var member in stateMachine.Members)
                if (member.IsRemote)
                    tasks.Add(AppendEntriesAsync(member, commitIndex, term, transactionLog, timerCancellation.Token)
                        .ContinueWith(HealthStatusContinuation, default, TaskContinuationOptions.ExecuteSynchronously,
                            TaskScheduler.Current));
            var votes = 1;  //because we know the entry is replicated in this node
            foreach (var task in tasks)
            {
                var result = await task.ConfigureAwait(false);
                if (result.Term > term)
                {
                    stateMachine.MoveToFollowerState(false, result.Term);
                    return false;
                }

                switch (result.Value)
                {
                    case MemberHealthStatus.Canceled: //leading was canceled
                        return false;
                    case MemberHealthStatus.Responded:
                        votes += 1;
                        break;
                    case MemberHealthStatus.Unavailable:
                        if (absoluteMajority)
                            votes -= 1;
                        break;
                }

                task.Dispose();
            }

            tasks.Clear();
            if (votes <= 0)
            {
                stateMachine.MoveToFollowerState(false);
                return false;
            }

            await transactionLog.CommitAsync(commitIndex +
                                             1); //commit all entries started from first uncommitted index to the end
            return true;
        }

        private async void DoHeartbeats(object transactionLog, bool timedOut)
        {
            if (IsDisposed || !timedOut || !processingState.FalseToTrue()) return;
            try
            {
                if (!await DoHeartbeats((IAuditTrail<ILogEntry>)transactionLog).ConfigureAwait(false))
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
        /// <param name="period">Time period of Heartbeats</param>
        /// <param name="transactionLog">Transaction log.</param>
        internal LeaderState StartLeading(int period, IAuditTrail<ILogEntry> transactionLog)
        {
            processingState.Value = false;
            if(transactionLog != null)
                foreach(var member in stateMachine.Members)
                    member.NextIndex = transactionLog.GetLastIndex(false) + 1;
            heartbeatTimer = ThreadPool.RegisterWaitForSingleObject(timerCancellation.Token.WaitHandle, DoHeartbeats,
                null, period, false);
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

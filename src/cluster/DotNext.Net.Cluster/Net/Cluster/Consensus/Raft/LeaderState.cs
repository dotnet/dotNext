using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using static Threading.Tasks.Conversion;
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
        private static readonly Converter<Result<bool>, long> ResultToTermConversion = result => result.Term;

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

        private static async Task<long> AppendEntriesAsync(IRaftClusterMember member, long currentIndex, long term, IAuditTrail<LogEntryId> transactionLog, CancellationToken token)
        {
            retry:
            var commitIndex = transactionLog.GetLastId(true).Index;
            var precedingEntry = await transactionLog.ResolveAsync(member.NextIndex - 1).ConfigureAwait(false) ?? transactionLog.Initial;
            var entries = currentIndex >= member.NextIndex ?
                await transactionLog.GetEntriesStartingFromAsync(member.NextIndex).ConfigureAwait(false) :
                Array.Empty<ILogEntry<LogEntryId>>();
            //trying to replicate
            var result = await member.AppendEntriesAsync(term, entries, precedingEntry, commitIndex, token).ConfigureAwait(false);
            if(result.Term > term)
                return result.Term;
            else if(result.Value)
            {
                member.NextIndex = currentIndex + 1;
                return result.Term;
            }
            else
            {
                member.NextIndex.DecrementAndGet();
                goto retry;
            }
        }

        private async Task<bool> DoHeartbeats(IAuditTrail<LogEntryId> transactionLog)
        {
            ICollection<Task<Result<MemberHealthStatus>>> tasks = new LinkedList<Task<Result<MemberHealthStatus>>>();
            //send heartbeat in parallel
            var currentIndex = transactionLog?.GetLastId(false).Index;
            foreach (var member in stateMachine.Members)
                tasks.Add((currentIndex.HasValue ? 
                    AppendEntriesAsync(member, currentIndex.Value, term, transactionLog, timerCancellation.Token) :
                    member.AppendEntriesAsync(term, Array.Empty<ILogEntry<LogEntryId>>(), default, long.MinValue, timerCancellation.Token).Convert(ResultToTermConversion)).ContinueWith(HealthStatusContinuation, default, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current));
            var votes = 0;
            foreach (var task in tasks)
            {
                var result = await task.ConfigureAwait(false);
                switch (result.Value)
                {
                    case MemberHealthStatus.Canceled:   //leading was canceled
                        return false;
                    case MemberHealthStatus.Responded:
                        if(result.Term > term)
                        {
                            stateMachine.MoveToFollowerState(false, result.Term);
                            return false;
                        }
                        votes += 1;
                        break;
                    case MemberHealthStatus.Unavailable:
                        if(absoluteMajority)
                            votes -= 1;
                        break;
                }
                task.Dispose();
            }
            tasks.Clear();
            if(votes <= 0)
            {
                stateMachine.MoveToFollowerState(false);
                return false;
            }
            if(currentIndex.HasValue)
                await transactionLog.CommitAsync(currentIndex.Value);   //commit all entries started from current index until the end
            return true;
        }

        private async void DoHeartbeats(object state, bool timedOut)
        {
            if (IsDisposed || !timedOut || !processingState.FalseToTrue()) return;
            try
            {
                if (!await DoHeartbeats(state as IAuditTrail<LogEntryId>).ConfigureAwait(false))
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
        internal LeaderState StartLeading(int delay, IAuditTrail<LogEntryId> transactionLog = null)
        {
            processingState.Value = false;
            if(transactionLog != null)
                foreach(var member in stateMachine.Members)
                    member.NextIndex = transactionLog.GetLastId(false).Index + 1;
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

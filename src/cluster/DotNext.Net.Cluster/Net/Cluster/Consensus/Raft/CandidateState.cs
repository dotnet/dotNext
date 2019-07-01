using DotNext.Net.Cluster.Replication;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal sealed class CandidateState : RaftState
    {
        private enum VotingResult : byte
        {
            Rejected = 0,
            Granted,
            Canceled,
            NotAvailable
        }

        private readonly struct VotingState
        {
            private static readonly Converter<bool, VotingResult> VotingResultConverter = result => result ? VotingResult.Granted : VotingResult.Rejected;

            private static readonly Func<Task<Result<bool>>, Result<VotingResult>> HandleTaskContinuation = task =>
            {
                if (task.IsCanceled)
                    return new Result<VotingResult>(long.MinValue, VotingResult.Canceled);
                if (task.IsFaulted)
                    return new Result<VotingResult>(long.MinValue, VotingResult.NotAvailable);
                return task.Result.Convert(VotingResultConverter);
            };

            internal readonly IRaftClusterMember Voter;
            internal readonly Task<Result<VotingResult>> Task;

            internal VotingState(IRaftClusterMember voter, long term, LogEntryId? lastRecord, CancellationToken token)
            {
                Voter = voter;
                Task = voter.VoteAsync(term, lastRecord, token).ContinueWith(HandleTaskContinuation, default, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current);
            }
        }

        private readonly CancellationTokenSource votingCancellation;
        internal readonly long Term;
        private volatile Task votingTask;
        private readonly bool absoluteMajority;

        internal CandidateState(IRaftStateMachine stateMachine, bool absoluteMajority, long term)
            : base(stateMachine)
        {
            votingCancellation = new CancellationTokenSource();
            Term = term;
            this.absoluteMajority = absoluteMajority;
        }

        private async Task EndVoting(IEnumerable<VotingState> voters)
        {
            var votes = 0;
            var localMember = default(IRaftClusterMember);
            foreach (var state in voters)
            {
                if (IsDisposed)
                    return;
                var result = await state.Task.ConfigureAwait(false);
                switch (result.Value)
                {
                    case VotingResult.Canceled: //candidate timeout happened
                        stateMachine.MoveToFollowerState(false);
                        return;
                    case VotingResult.Granted:
                        stateMachine.Logger.VoteGranted(state.Voter.Endpoint);
                        votes += 1;
                        break; 
                    case VotingResult.Rejected:
                        stateMachine.Logger.VoteRejected(state.Voter.Endpoint);
                        if(result.Term > Term)  //current node is outdated
                        {
                            stateMachine.MoveToFollowerState(false, result.Term);
                            return;
                        }
                        votes -= 1;
                        break;
                    case VotingResult.NotAvailable:
                        stateMachine.Logger.MemberUnavailable(state.Voter.Endpoint);
                        if (absoluteMajority)
                            votes -= 1;
                        break;
                }

                state.Task.Dispose();
                if (!state.Voter.IsRemote)
                    localMember = state.Voter;
            }
            stateMachine.Logger.VotingCompleted(votes);
            if (!votingCancellation.IsCancellationRequested && votes > 0 && localMember != null)
                stateMachine.MoveToLeaderState(localMember); //becomes a leader
            else
                stateMachine.MoveToFollowerState(true); //no clear consensus
        }

        /// <summary>
        /// Starts voting asynchronously.
        /// </summary>
        /// <param name="timeout">Candidate state timeout.</param>
        /// <param name="auditTrail">The local transaction log.</param>
        internal CandidateState StartVoting(int timeout, IAuditTrail<LogEntryId> auditTrail = null)
        {
            stateMachine.Logger.VotingStarted(timeout);
            ICollection<VotingState> voters = new LinkedList<VotingState>();
            votingCancellation.CancelAfter(timeout);
            //start voting in parallel
            var lastRecord = auditTrail?.GetLastId(false);
            foreach (var member in stateMachine.Members)
                voters.Add(new VotingState(member, Term, lastRecord, votingCancellation.Token));
            votingTask = EndVoting(voters);
            return this;
        }

        /// <summary>
        /// Cancels candidate state.
        /// </summary>
        internal Task StopVoting()
        {
            votingCancellation.Cancel();
            return votingTask ?? Task.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                votingCancellation.Dispose();
                var task = Interlocked.Exchange(ref votingTask, null);
                if (task != null && task.IsCompleted)
                    task.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

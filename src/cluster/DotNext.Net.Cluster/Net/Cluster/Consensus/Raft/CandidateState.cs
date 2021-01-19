﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO.Log;
    using static Threading.Tasks.Continuation;

    internal sealed class CandidateState : RaftState
    {
        private enum VotingResult : byte
        {
            Rejected = 0,
            Granted,
            Canceled,
            NotAvailable,
        }

        [StructLayout(LayoutKind.Auto)]
        private readonly struct VotingState
        {
            internal readonly IRaftClusterMember Voter;
            internal readonly Task<Result<VotingResult>> Task;

            private static async Task<Result<VotingResult>> VoteAsync(IRaftClusterMember voter, long term, IAuditTrail<IRaftLogEntry> auditTrail, CancellationToken token)
            {
                var lastIndex = auditTrail.GetLastIndex(false);
                var lastTerm = await auditTrail.GetTermAsync(lastIndex, token).ConfigureAwait(false);
                VotingResult result;
                try
                {
                    var response = await voter.VoteAsync(term, lastIndex, lastTerm, token).ConfigureAwait(false);
                    term = response.Term;
                    result = response.Value ? VotingResult.Granted : VotingResult.Rejected;
                }
                catch (OperationCanceledException)
                {
                    result = VotingResult.Canceled;
                }
                catch (MemberUnavailableException)
                {
                    result = VotingResult.NotAvailable;
                    term = -1L;
                }

                return new Result<VotingResult>(term, result);
            }

            internal VotingState(IRaftClusterMember voter, long term, IAuditTrail<IRaftLogEntry> auditTrail, CancellationToken token)
            {
                Voter = voter;
                Task = VoteAsync(voter, term, auditTrail, token);
            }
        }

        private readonly CancellationTokenSource votingCancellation;
        internal readonly long Term;
        private Task? votingTask;

        internal CandidateState(IRaftStateMachine stateMachine, long term)
            : base(stateMachine)
        {
            votingCancellation = new CancellationTokenSource();
            Term = term;
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

                // current node is outdated
                if (result.Term > Term)
                {
                    stateMachine.MoveToFollowerState(false, result.Term);
                    return;
                }

                switch (result.Value)
                {
                    case VotingResult.Canceled: // candidate timeout happened
                        stateMachine.MoveToFollowerState(false);
                        return;
                    case VotingResult.Granted:
                        stateMachine.Logger.VoteGranted(state.Voter.EndPoint);
                        votes += 1;
                        break;
                    case VotingResult.Rejected:
                        stateMachine.Logger.VoteRejected(state.Voter.EndPoint);
                        votes -= 1;
                        break;
                    case VotingResult.NotAvailable:
                        stateMachine.Logger.MemberUnavailable(state.Voter.EndPoint);
                        votes -= 1;
                        break;
                }

                state.Task.Dispose();
                if (!state.Voter.IsRemote)
                    localMember = state.Voter;
            }

            stateMachine.Logger.VotingCompleted(votes);
            if (votingCancellation.IsCancellationRequested || votes <= 0 || localMember is null)
                stateMachine.MoveToFollowerState(true); // no clear consensus
            else
                stateMachine.MoveToLeaderState(localMember); // becomes a leader
        }

        /// <summary>
        /// Starts voting asynchronously.
        /// </summary>
        /// <param name="timeout">Candidate state timeout.</param>
        /// <param name="auditTrail">The local transaction log.</param>
        internal CandidateState StartVoting(int timeout, IAuditTrail<IRaftLogEntry> auditTrail)
        {
            stateMachine.Logger.VotingStarted(timeout);
            ICollection<VotingState> voters = new LinkedList<VotingState>();
            votingCancellation.CancelAfter(timeout);

            // start voting in parallel
            foreach (var member in stateMachine.Members)
                voters.Add(new VotingState(member, Term, auditTrail, votingCancellation.Token));
            votingTask = EndVoting(voters);
            return this;
        }

        /// <summary>
        /// Cancels candidate state.
        /// </summary>
        internal override Task StopAsync()
        {
            votingCancellation.Cancel();
            return votingTask?.OnCompleted() ?? Task.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                votingCancellation.Dispose();
                var task = Interlocked.Exchange(ref votingTask, null);
                if (task is not null && task.IsCompleted)
                    task.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}

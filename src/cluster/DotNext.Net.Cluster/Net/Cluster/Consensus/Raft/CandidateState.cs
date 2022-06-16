using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

using IO.Log;

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
            var lastIndex = auditTrail.LastUncommittedEntryIndex;
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

            return new(term, result);
        }

        internal VotingState(IRaftClusterMember voter, long term, IAuditTrail<IRaftLogEntry> auditTrail, CancellationToken token)
        {
            Voter = voter;

            // ensure parallel requesting of votes
            Task = System.Threading.Tasks.Task.Run(() => VoteAsync(voter, term, auditTrail, token));
        }
    }

    private readonly CancellationTokenSource votingCancellation;
    internal readonly long Term;
    private Task? votingTask;

    internal CandidateState(IRaftStateMachine stateMachine, long term)
        : base(stateMachine)
    {
        votingCancellation = new();
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
                MoveToFollowerState(randomizeTimeout: false, result.Term);
                return;
            }

            switch (result.Value)
            {
                case VotingResult.Canceled: // candidate timeout happened
                    MoveToFollowerState(randomizeTimeout: false);
                    return;
                case VotingResult.Granted:
                    Logger.VoteGranted(state.Voter.EndPoint);
                    votes += 1;
                    break;
                case VotingResult.Rejected:
                    Logger.VoteRejected(state.Voter.EndPoint);
                    votes -= 1;
                    break;
                case VotingResult.NotAvailable:
                    Logger.MemberUnavailable(state.Voter.EndPoint);
                    votes -= 1;
                    break;
            }

            state.Task.Dispose();
            if (!state.Voter.IsRemote)
                localMember = state.Voter;
        }

        Logger.VotingCompleted(votes);
        if (votingCancellation.IsCancellationRequested || votes <= 0 || localMember is null)
            MoveToFollowerState(randomizeTimeout: true); // no clear consensus
        else
            MoveToLeaderState(localMember); // becomes a leader
    }

    /// <summary>
    /// Starts voting asynchronously.
    /// </summary>
    /// <param name="timeout">Candidate state timeout.</param>
    /// <param name="auditTrail">The local transaction log.</param>
    internal void StartVoting(int timeout, IAuditTrail<IRaftLogEntry> auditTrail)
    {
        Logger.VotingStarted(timeout);
        var members = Members;
        var voters = new List<VotingState>(members.Count);

        // start voting in parallel
        foreach (var member in members)
            voters.Add(new(member, Term, auditTrail, votingCancellation.Token));

        votingCancellation.CancelAfter(timeout);
        votingTask = EndVoting(voters);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        try
        {
            votingCancellation.Cancel();
            await (votingTask ?? Task.CompletedTask).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Logger.CandidateStateExitedWithError(e);
        }
        finally
        {
            Dispose(true);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            votingCancellation.Dispose();
        }

        base.Dispose(disposing);
    }
}
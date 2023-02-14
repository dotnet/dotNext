using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

using IO.Log;
using Runtime.CompilerServices;
using Threading.Tasks;

internal sealed class CandidateState<TMember> : RaftState<TMember>
    where TMember : class, IRaftClusterMember
{
    private enum VotingResult : byte
    {
        Rejected = 0,
        Granted,
        Canceled,
        NotAvailable,
    }

    private readonly CancellationTokenSource votingCancellation;
    internal readonly long Term;
    private Task? votingTask;

    internal CandidateState(IRaftStateMachine<TMember> stateMachine, long term)
        : base(stateMachine)
    {
        votingCancellation = new();
        Term = term;
    }

    private async Task EndVoting(IAsyncEnumerable<(TMember, long, VotingResult)> voters)
    {
        var votes = 0;
        var localMember = default(TMember);
        await foreach (var (member, term, result) in voters.ConfigureAwait(false))
        {
            if (IsDisposingOrDisposed)
                return;

            // current node is outdated
            if (term > Term)
            {
                MoveToFollowerState(randomizeTimeout: false, term);
                return;
            }

            switch (result)
            {
                case VotingResult.Canceled: // candidate timeout happened
                    MoveToFollowerState(randomizeTimeout: false);
                    return;
                case VotingResult.Granted:
                    Logger.VoteGranted(member.EndPoint);
                    votes += 1;
                    break;
                case VotingResult.Rejected:
                    Logger.VoteRejected(member.EndPoint);
                    votes -= 1;
                    break;
                case VotingResult.NotAvailable:
                    Logger.MemberUnavailable(member.EndPoint);
                    votes -= 1;
                    break;
            }

            if (!member.IsRemote)
                localMember = member;
        }

        Logger.VotingCompleted(votes, Term);
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
        Logger.VotingStarted(timeout, Term);
        var voters = new TaskCompletionPipe<Task<(TMember, long, VotingResult)>>();

        // start voting in parallel
        foreach (var member in Members)
            voters.Add(VoteAsync(member, Term, auditTrail, votingCancellation.Token));

        voters.Complete();
        votingCancellation.CancelAfter(timeout);
        votingTask = EndVoting(voters.GetConsumer());

        [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder<>))]
        static async Task<(TMember, long, VotingResult)> VoteAsync(TMember voter, long term, IAuditTrail<IRaftLogEntry> auditTrail, CancellationToken token)
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

            return (voter, term, result);
        }
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
            Dispose(disposing: true);
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

internal static class CandidateState
{
    internal static readonly Counter<int> TransitionRateMeter = Metrics.Instrumentation.ServerSide.CreateCounter<int>("transitions-to-candidate-count");
}
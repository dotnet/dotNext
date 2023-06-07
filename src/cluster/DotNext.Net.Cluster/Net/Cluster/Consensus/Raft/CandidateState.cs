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

    private async Task VoteAsync(int timeout, IAuditTrail<IRaftLogEntry> auditTrail)
    {
        // Perf: reuse index and related term once for all members
        var lastIndex = auditTrail.LastUncommittedEntryIndex;
        var lastTerm = await auditTrail.GetTermAsync(lastIndex, votingCancellation.Token).ConfigureAwait(false);

        // start voting in parallel
        var voters = StartVoting(Members, Term, lastIndex, lastTerm, votingCancellation.Token);
        votingCancellation.CancelAfter(timeout);

        // finish voting
        await EndVoting(voters.GetConsumer(), votingCancellation.Token).ConfigureAwait(false);

        static TaskCompletionPipe<Task<(TMember, long, VotingResult)>> StartVoting(IReadOnlyCollection<TMember> members, long currentTerm, long lastIndex, long lastTerm, CancellationToken token)
        {
            var voters = new TaskCompletionPipe<Task<(TMember, long, VotingResult)>>();

            // start voting in parallel
            foreach (var member in members)
                voters.Add(VoteAsync(member, currentTerm, lastIndex, lastTerm, token));

            voters.Complete();
            return voters;
        }

        [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder<>))]
        static async Task<(TMember, long, VotingResult)> VoteAsync(TMember voter, long currentTerm, long lastIndex, long lastTerm, CancellationToken token)
        {
            VotingResult result;
            try
            {
                var response = await voter.VoteAsync(currentTerm, lastIndex, lastTerm, token).ConfigureAwait(false);
                currentTerm = response.Term;
                result = response.Value ? VotingResult.Granted : VotingResult.Rejected;
            }
            catch (OperationCanceledException)
            {
                result = VotingResult.Canceled;
            }
            catch (MemberUnavailableException)
            {
                result = VotingResult.NotAvailable;
                currentTerm = -1L;
            }

            return (voter, currentTerm, result);
        }
    }

    private async Task EndVoting(IAsyncEnumerable<(TMember, long, VotingResult)> voters, CancellationToken token)
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
        if (token.IsCancellationRequested || votes <= 0 || localMember is null)
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
        votingTask = VoteAsync(timeout, auditTrail);
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
    internal static readonly Counter<int> TransitionRateMeter = Metrics.Instrumentation.ServerSide.CreateCounter<int>("transitions-to-candidate-count", description: "Number of Transitions to Candidate State");
}
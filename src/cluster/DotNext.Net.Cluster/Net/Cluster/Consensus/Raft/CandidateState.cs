using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Runtime.CompilerServices;

internal sealed class CandidateState<TMember> : RaftState<TMember>
    where TMember : class, IRaftClusterMember
{
    private readonly CancellationTokenSource votingCancellation;
    private readonly CancellationToken votingCancellationToken; // cached to prevent ObjectDisposedException
    private Task? votingTask;

    public CandidateState(IRaftStateMachine<TMember> stateMachine)
        : base(stateMachine)
    {
        votingCancellation = new();
        votingCancellationToken = votingCancellation.Token;
    }

    internal required long Term
    {
        get;
        init;
    }

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task VoteAsync(TimeSpan timeout)
    {
        // Perf: reuse index and related term once for all members
        var lastIndex = AuditTrail.LastEntryIndex;
        var lastTerm = await AuditTrail.GetTermAsync(lastIndex, votingCancellationToken).ConfigureAwait(false);

        // start voting in parallel
        var voters = StartVoting(lastIndex, lastTerm);
        votingCancellation.CancelAfter(timeout);

        // finish voting
        await EndVoting(voters).ConfigureAwait(false);
    }
    
    private IAsyncEnumerable<Task<(TMember, long, bool?)>> StartVoting(long lastIndex, long lastTerm)
        => Task.WhenEach(Members
            .TakeWhile(NotCanceled)
            .Select(member => VoteAsync(member, lastIndex, lastTerm))
        );

    private bool NotCanceled(TMember _) => !votingCancellation.IsCancellationRequested;

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder<>))]
    private async Task<(TMember, long, bool?)> VoteAsync(TMember voter, long lastIndex, long lastTerm)
    {
        bool? result;
        long currentTerm;
        try
        {
            var response = await voter.VoteAsync(Term, lastIndex, lastTerm, votingCancellationToken).ConfigureAwait(false);
            currentTerm = response.Term;
            result = response.Value;
        }
        catch (MemberUnavailableException)
        {
            result = null;
            currentTerm = -1L;
        }

        return (voter, currentTerm, result);
    }

    private async Task EndVoting(IAsyncEnumerable<Task<(TMember, long, bool?)>> voters)
    {
        var votes = 0;
        var localMember = default(TMember);

        var enumerator = voters.GetAsyncEnumerator(votingCancellationToken);
        try
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                var (member, term, result) = await enumerator.Current.ConfigureAwait(false);

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
                    case true:
                        Logger.VoteGranted(member.EndPoint);
                        votes += 1;
                        break;
                    case false:
                        Logger.VoteRejected(member.EndPoint);
                        votes -= 1;
                        break;
                    default:
                        Logger.MemberUnavailable(member.EndPoint);
                        votes -= 1;
                        break;
                }

                if (!member.IsRemote)
                    localMember = member;
            }
        }
        catch (OperationCanceledException)
        {
            // candidate timeout happened
            MoveToFollowerState(randomizeTimeout: false);
            return;
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }

        Logger.VotingCompleted(votes, Term);
        if (!TryReset() || votes <= 0 || localMember is null)
        {
            MoveToFollowerState(randomizeTimeout: true); // no clear consensus
        }
        else
        {
            // becomes a leader
            MoveToLeaderState(
                localMember,
                await AuditTrail.AppendAsync(new EmptyLogEntry { Term = Term }, votingCancellationToken).ConfigureAwait(false));
        }
    }

    private bool TryReset()
    {
        bool result;
        try
        {
            result = votingCancellation.TryReset();
        }
        catch (ObjectDisposedException)
        {
            result = false;
        }

        return result;
    }

    /// <summary>
    /// Starts voting asynchronously.
    /// </summary>
    /// <param name="timeout">Candidate state timeout.</param>
    internal void StartVoting(TimeSpan timeout)
    {
        CandidateState.TransitionRateMeter.Add(1, in MeasurementTags);
        Logger.VotingStarted(timeout, Term);
        votingTask = VoteAsync(timeout);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        try
        {
            votingCancellation.Cancel(throwOnFirstException: false);
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
            votingTask = null;
        }

        base.Dispose(disposing);
    }
}

file static class CandidateState
{
    internal static readonly Counter<int> TransitionRateMeter = Metrics.Instrumentation.ServerSide.CreateCounter<int>("transitions-to-candidate-count", description: "Number of Transitions to Candidate State");
}
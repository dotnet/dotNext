using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

using IO.Log;
using Runtime.CompilerServices;
using Threading.Tasks;

internal sealed class CandidateState<TMember> : RaftState<TMember>
    where TMember : class, IRaftClusterMember
{
    private readonly CancellationTokenSource votingCancellation;
    private readonly CancellationToken votingCancellationToken; // cached to prevent ObjectDisposedException
    internal readonly long Term;
    private Task? votingTask;

    public CandidateState(IRaftStateMachine<TMember> stateMachine, long term)
        : base(stateMachine)
    {
        Term = term;
        votingCancellation = new();
        votingCancellationToken = votingCancellation.Token;
    }

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task VoteAsync(TimeSpan timeout, IAuditTrail<IRaftLogEntry> auditTrail)
    {
        // Perf: reuse index and related term once for all members
        var lastIndex = auditTrail.LastEntryIndex;
        var lastTerm = await auditTrail.GetTermAsync(lastIndex, votingCancellationToken).ConfigureAwait(false);

        // start voting in parallel
        var voters = StartVoting(Members, Term, lastIndex, lastTerm, votingCancellationToken);
        votingCancellation.CancelAfter(timeout);

        // finish voting
        await EndVoting(voters.GetConsumer(), votingCancellation.Token).ConfigureAwait(false);

        static TaskCompletionPipe<Task<(TMember, long, bool?)>> StartVoting(IReadOnlyCollection<TMember> members, long currentTerm, long lastIndex, long lastTerm, CancellationToken token)
        {
            var voters = new TaskCompletionPipe<Task<(TMember, long, bool?)>>();

            // start voting in parallel
            using (var enumerator = members.GetEnumerator())
            {
                while (enumerator.MoveNext() && !token.IsCancellationRequested)
                {
                    voters.Add(VoteAsync(enumerator.Current, currentTerm, lastIndex, lastTerm, token));
                }
            }

            voters.Complete();
            return voters;
        }

        [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder<>))]
        static async Task<(TMember, long, bool?)> VoteAsync(TMember voter, long currentTerm, long lastIndex, long lastTerm, CancellationToken token)
        {
            bool? result;
            try
            {
                var response = await voter.VoteAsync(currentTerm, lastIndex, lastTerm, token).ConfigureAwait(false);
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
    }

    private async Task EndVoting(TaskCompletionPipe.Consumer<(TMember, long, bool?)> voters, CancellationToken token)
    {
        var votes = 0;
        var localMember = default(TMember);

        var enumerator = voters.GetAsyncEnumerator(token);
        try
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                var (member, term, result) = enumerator.Current;

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
        if (token.IsCancellationRequested || votes <= 0 || localMember is null)
        {
            MoveToFollowerState(randomizeTimeout: true); // no clear consensus
        }
        else
        {
            MoveToLeaderState(localMember); // becomes a leader
        }
    }

    /// <summary>
    /// Starts voting asynchronously.
    /// </summary>
    /// <param name="timeout">Candidate state timeout.</param>
    /// <param name="auditTrail">The local transaction log.</param>
    internal void StartVoting(TimeSpan timeout, IAuditTrail<IRaftLogEntry> auditTrail)
    {
        CandidateState.TransitionRateMeter.Add(1, in MeasurementTags);
        Logger.VotingStarted(timeout, Term);
        votingTask = VoteAsync(timeout, auditTrail);
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
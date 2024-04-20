using System.Diagnostics.Metrics;

namespace DotNext.Net.Cluster.Consensus.Raft;

internal sealed class FollowerState<TMember> : ConsensusTrackerState<TMember>
    where TMember : class, IRaftClusterMember
{
    private readonly CancellationTokenSource trackerCancellation;
    private Task? tracker;
    private volatile bool timedOut;

    public FollowerState(IRaftStateMachine<TMember> stateMachine)
        : base(stateMachine)
    {
        trackerCancellation = new();
        Token = trackerCancellation.Token;
    }

    public override CancellationToken Token { get; } // cached to prevent ObjectDisposedException

    private async Task Track(TimeSpan timeout)
    {
        // spin loop to wait for the timeout
        while (await refreshEvent.WaitAsync(timeout, Token).ConfigureAwait(false))
        {
            // Transition can be suppressed. If so, resume the loop and reset the timer.
            // If the event is in signaled state then the returned task is completed synchronously.
            await suppressionEvent.WaitAsync(Token).ConfigureAwait(false);
        }

        timedOut = true;

        // Timeout happened, move to candidate state.
        // However, at this point, the cluster may receive Vote request which calls Refresh() method
        // and turns refreshEvent into signaled state.
        // In that case, we have a race condition between Follower and future Candidate state
        // (because transition to Candidate state is scheduled via ThreadPool asynchronously).
        // To resolve the issue, inside of transition handler we must check refreshEvent state.
        // If it is in signaled state, resume following and do not move to Candidate state.
        // See: https://github.com/dotnet/dotNext/issues/168
        MoveToCandidateState();
    }

    internal void StartServing(TimeSpan timeout)
    {
        refreshEvent.Reset();
        timedOut = false;
        tracker = Track(timeout);

        FollowerState.TransitionRateMeter.Add(1, in MeasurementTags);
    }

    internal bool IsExpired => timedOut;

    public override void Refresh()
    {
        refreshEvent.Set();
        base.Refresh();
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        try
        {
            trackerCancellation.Cancel(throwOnFirstException: false);
            await (tracker ?? Task.CompletedTask).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Logger.FollowerStateExitedWithError(e);
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
            trackerCancellation.Dispose();
            tracker = null;
        }

        base.Dispose(disposing);
    }
}

file static class FollowerState
{
    internal static readonly Counter<int> TransitionRateMeter = Metrics.Instrumentation.ServerSide.CreateCounter<int>("transitions-to-follower-count", description: "Number of Transitions to Follower State");
}
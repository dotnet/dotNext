using System.Diagnostics.Metrics;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Threading;

internal sealed class FollowerState<TMember> : RaftState<TMember>
    where TMember : class, IRaftClusterMember
{
    private readonly AsyncAutoResetEvent refreshEvent;
    private readonly AsyncManualResetEvent suppressionEvent;
    private readonly CancellationTokenSource trackerCancellation;
    private Task? tracker;
    internal IFollowerStateMetrics? Metrics;
    private volatile bool timedOut;

    internal FollowerState(IRaftStateMachine<TMember> stateMachine)
        : base(stateMachine)
    {
        refreshEvent = new(initialState: false) { MeasurementTags = stateMachine.MeasurementTags };
        suppressionEvent = new(initialState: true) { MeasurementTags = stateMachine.MeasurementTags };
        trackerCancellation = new();
    }

    private void SuspendTracking()
    {
        suppressionEvent.Reset();
        refreshEvent.Set();
    }

    private void ResumeTracking() => suppressionEvent.Set();

    private async Task Track(TimeSpan timeout, CancellationToken token)
    {
        Debug.Assert(token != trackerCancellation.Token);

        using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, trackerCancellation.Token);

        // spin loop to wait for the timeout
        while (await refreshEvent.WaitAsync(timeout, tokenSource.Token).ConfigureAwait(false))
        {
            // Transition can be suppressed. If so, resume the loop and reset the timer.
            // If the event is in signaled state then the returned task is completed synchronously.
            await suppressionEvent.WaitAsync(tokenSource.Token).ConfigureAwait(false);
        }

        timedOut = true;

        // timeout happened, move to candidate state
        MoveToCandidateState();
    }

    internal void StartServing(TimeSpan timeout, CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            trackerCancellation.Cancel(false);
            tracker = null;
        }
        else
        {
            refreshEvent.Reset();
            timedOut = false;
            tracker = Track(timeout, token);
        }
    }

    internal bool IsExpired => timedOut;

    internal bool IsRefreshRequested => refreshEvent.IsSet;

    internal void Refresh()
    {
        Logger.TimeoutReset();
        refreshEvent.Set();
        Metrics?.ReportHeartbeat();
        FollowerState.HeartbeatRateMeter.Add(1, MeasurementTags);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        try
        {
            trackerCancellation.Cancel(false);
            await (tracker ?? Task.CompletedTask).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Logger.FollowerStateExitedFailed(e);
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
            refreshEvent.Dispose();
            suppressionEvent.Dispose();
            trackerCancellation.Dispose();
            tracker = null;
            Metrics = null;
        }

        base.Dispose(disposing);
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct TransitionSuppressionScope : IDisposable
    {
        private readonly FollowerState<TMember>? state;

        internal TransitionSuppressionScope(FollowerState<TMember>? state)
        {
            state?.SuspendTracking();
            this.state = state;
        }

        public void Dispose() => state?.ResumeTracking();
    }
}

internal static class FollowerState
{
    internal static readonly Counter<int> TransitionRateMeter = Metrics.Instrumentation.ServerSide.CreateCounter<int>("transitions-to-follower-count", description: "Number of Transitions to Follower State");
    internal static readonly Counter<int> HeartbeatRateMeter = Metrics.Instrumentation.ServerSide.CreateCounter<int>("incoming-heartbeats-count", description: "Incoming Heartbeats from Leader");
}
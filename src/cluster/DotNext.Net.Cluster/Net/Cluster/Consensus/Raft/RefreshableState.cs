using System.Diagnostics.Metrics;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Threading;

internal abstract class RefreshableState<TMember>(IRaftStateMachine<TMember> stateMachine) : ConsensusState<TMember>(stateMachine)
    where TMember : class, IRaftClusterMember
{
    protected readonly AsyncAutoResetEvent refreshEvent = new(initialState: false) { MeasurementTags = stateMachine.MeasurementTags };
    protected readonly AsyncManualResetEvent suppressionEvent = new(initialState: true) { MeasurementTags = stateMachine.MeasurementTags };

    public virtual void Refresh()
    {
        Logger.TimeoutReset();
        ConsensusTrackerState.HeartbeatRateMeter.Add(1, MeasurementTags);
    }

    public bool IsRefreshRequested => refreshEvent.IsSet;

    private void SuspendTracking()
    {
        suppressionEvent.Reset();
        refreshEvent.Set();
    }

    private void ResumeTracking() => suppressionEvent.Set();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            refreshEvent.Dispose();
            suppressionEvent.Dispose();
        }

        base.Dispose(disposing);
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct TransitionSuppressionScope : IDisposable
    {
        private readonly RefreshableState<TMember>? state;

        internal TransitionSuppressionScope(RefreshableState<TMember>? state)
        {
            state?.SuspendTracking();
            this.state = state;
        }

        public void Dispose() => state?.ResumeTracking();
    }
}

file static class ConsensusTrackerState
{
    internal static readonly Counter<int> HeartbeatRateMeter = Metrics.Instrumentation.ServerSide.CreateCounter<int>("incoming-heartbeats-count", description: "Incoming Heartbeats from Leader");
}
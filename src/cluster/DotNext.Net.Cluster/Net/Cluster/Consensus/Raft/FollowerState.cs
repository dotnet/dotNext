using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Threading;
using static Threading.Tasks.Continuation;

internal sealed class FollowerState : RaftState
{
    private readonly AsyncAutoResetEvent refreshEvent;
    private readonly AsyncManualResetEvent suppressionEvent;
    private readonly CancellationTokenSource trackerCancellation;
    private Task? tracker;
    internal IFollowerStateMetrics? Metrics;
    private volatile bool timedOut;

    internal FollowerState(IRaftStateMachine stateMachine)
        : base(stateMachine)
    {
        refreshEvent = new AsyncAutoResetEvent(initialState: false);
        suppressionEvent = new AsyncManualResetEvent(initialState: true);
        trackerCancellation = new CancellationTokenSource();
    }

    private void SuspendTracking()
    {
        suppressionEvent.Reset();
        refreshEvent.Set();
    }

    private void ResumeTracking() => suppressionEvent.Set();

    private async Task Track(TimeSpan timeout, IAsyncEvent refreshEvent, CancellationToken token1, CancellationToken token2)
    {
        using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token1, token2);

        // spin loop to wait for the timeout
        while (await refreshEvent.WaitAsync(timeout, tokenSource.Token).ConfigureAwait(false))
        {
            // Transition can be suppressed. If so, resume the loop and reset the timer.
            // If the event is in signaled state then the returned task is completed synchronously.
            await suppressionEvent.WaitAsync(tokenSource.Token).ConfigureAwait(false);
        }

        timedOut = true;

        // timeout happened, move to candidate state
        ThreadPool.UnsafeQueueUserWorkItem(MoveToCandidateStateWorkItem(), preferLocal: true);
    }

    internal FollowerState StartServing(TimeSpan timeout, CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            trackerCancellation.Cancel(false);
            tracker = null;
        }
        else
        {
            timedOut = false;
            tracker = Track(timeout, refreshEvent, trackerCancellation.Token, token);
        }

        return this;
    }

    internal bool IsExpired => timedOut;

    internal override Task StopAsync()
    {
        trackerCancellation.Cancel(false);
        return tracker?.OnCompleted() ?? Task.CompletedTask;
    }

    internal void Refresh()
    {
        Logger.TimeoutReset();
        refreshEvent.Set();
        Metrics?.ReportHeartbeat();
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
        private readonly FollowerState? state;

        internal TransitionSuppressionScope(FollowerState? state)
        {
            state?.SuspendTracking();
            this.state = state;
        }

        public void Dispose() => state?.ResumeTracking();
    }
}
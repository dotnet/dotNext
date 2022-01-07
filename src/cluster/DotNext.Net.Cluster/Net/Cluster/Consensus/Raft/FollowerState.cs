namespace DotNext.Net.Cluster.Consensus.Raft;

using Threading;
using static Threading.Tasks.Continuation;

internal sealed class FollowerState : RaftState
{
    private readonly IAsyncEvent refreshEvent;
    private readonly CancellationTokenSource trackerCancellation;
    private Task? tracker;
    internal IFollowerStateMetrics? Metrics;
    private volatile bool timedOut;

    internal FollowerState(IRaftStateMachine stateMachine)
        : base(stateMachine)
    {
        refreshEvent = new AsyncAutoResetEvent(false);
        trackerCancellation = new CancellationTokenSource();
    }

    private async Task Track(TimeSpan timeout, IAsyncEvent refreshEvent, CancellationToken token1, CancellationToken token2)
    {
        using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token1, token2);

        // spin loop to wait for the timeout
        while (await refreshEvent.WaitAsync(timeout, tokenSource.Token).ConfigureAwait(false));

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
        refreshEvent.Signal();
        Metrics?.ReportHeartbeat();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            refreshEvent.Dispose();
            trackerCancellation.Dispose();
            tracker = null;
            Metrics = null;
        }

        base.Dispose(disposing);
    }
}
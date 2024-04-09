using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft;

/// <summary>
/// This is ephemeral state indicating that
/// the cluster member will not become a leader.
/// </summary>
internal sealed class StandbyState<TMember> : ConsensusTrackerState<TMember>
    where TMember : class, IRaftClusterMember
{
    private readonly TimeSpan consensusTimeout;
    private readonly CancellationTokenSource lifecycleTokenSource;
    private readonly CancellationToken lifecycleToken;

    [SuppressMessage("Usage", "CA2213", Justification = "Disposed using DestroyLease() method")]
    private volatile ConsensusTokenSource? consensusTokenSource;
    private Task? tracker;

    internal StandbyState(IRaftStateMachine<TMember> stateMachine, TimeSpan consensusTimeout)
        : base(stateMachine)
    {
        lifecycleTokenSource = new();
        lifecycleToken = lifecycleTokenSource.Token;
        this.consensusTimeout = consensusTimeout;
    }

    public override CancellationToken Token => consensusTokenSource?.Token ?? new(canceled: true);

    internal bool Resumable { get; init; } = true;

    public override void Refresh()
    {
        if (tracker is null or { IsCompletedSuccessfully: true } || consensusTokenSource is null)
        {
            tracker = Track();
        }
        else
        {
            refreshEvent.Set();
        }

        base.Refresh();
    }

    private async Task Track()
    {
        consensusTokenSource = new();

        try
        {
            // spin loop to wait for the timeout
            while (await refreshEvent.WaitAsync(consensusTimeout, lifecycleToken).ConfigureAwait(false))
            {
                // Transition can be suppressed. If so, resume the loop and reset the timer.
                // If the event is in signaled state then the returned task is completed synchronously.
                await suppressionEvent.WaitAsync(lifecycleToken).ConfigureAwait(false);
            }
        }
        finally
        {
            using var cts = Interlocked.Exchange(ref consensusTokenSource, null);
            cts.Cancel(throwOnFirstException: false);
            cts.Dispose();
        }

        // Ignored if timeout tracking is aborted by OperationCanceledException.
        // This could happen if the state is disposed asynchronously due to transition to another state.
        IncomingHeartbeatTimedOut();
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        try
        {
            lifecycleTokenSource.Cancel(throwOnFirstException: false);
            await (tracker ?? Task.CompletedTask).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Logger.StandbyStateExitedWithError(e);
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
            lifecycleTokenSource.Dispose();
            Interlocked.Exchange(ref consensusTokenSource, null)?.Dispose();
            tracker = null;
        }

        base.Dispose(disposing);
    }

    private sealed class ConsensusTokenSource : CancellationTokenSource
    {
        internal readonly new CancellationToken Token; // cached to avoid ObjectDisposedException

        internal ConsensusTokenSource() => Token = base.Token;
    }
}
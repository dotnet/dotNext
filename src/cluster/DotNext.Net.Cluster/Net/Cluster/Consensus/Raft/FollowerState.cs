using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Threading;
    using static Threading.Tasks.Continuation;

    internal sealed class FollowerState : RaftState
    {
        private readonly IAsyncEvent refreshEvent;
        private readonly CancellationTokenSource trackerCancellation;
        private Task? tracker;
        internal IFollowerStateMetrics? Metrics;

        internal FollowerState(IRaftStateMachine stateMachine)
            : base(stateMachine)
        {
            refreshEvent = new AsyncAutoResetEvent(false);
            trackerCancellation = new CancellationTokenSource();
        }

        private static async Task Track(TimeSpan timeout, IAsyncEvent refreshEvent, Action candidateState, params CancellationToken[] tokens)
        {
            using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(tokens);

            // spin loop to wait for the timeout
            while (await refreshEvent.WaitAsync(timeout, tokenSource.Token).ConfigureAwait(false))
            {
            }

            // timeout happened, move to candidate state
            candidateState();
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
                tracker = Track(timeout, refreshEvent, stateMachine.MoveToCandidateState, trackerCancellation.Token, token);
            }

            return this;
        }

        internal override Task StopAsync()
        {
            trackerCancellation.Cancel(false);
            return tracker?.OnCompleted() ?? Task.CompletedTask;
        }

        internal void Refresh()
        {
            stateMachine.Logger.TimeoutReset();
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
            }

            base.Dispose(disposing);
        }
    }
}

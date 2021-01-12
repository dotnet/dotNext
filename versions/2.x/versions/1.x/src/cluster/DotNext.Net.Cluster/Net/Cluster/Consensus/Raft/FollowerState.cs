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
        private Task tracker;
        internal IFollowerStateMetrics Metrics;

        internal FollowerState(IRaftStateMachine stateMachine)
            : base(stateMachine)
        {
            refreshEvent = new AsyncAutoResetEvent(false);
            trackerCancellation = new CancellationTokenSource();
        }

        private static async Task Track(TimeSpan timeout, IAsyncEvent refreshEvent, Action candidateState, CancellationToken token)
        {
            //spin loop to wait for the timeout
            while (await refreshEvent.Wait(timeout, token).ConfigureAwait(false)) { }
            //timeout happened, move to candidate state
            candidateState();
        }

        internal FollowerState StartServing(TimeSpan timeout)
        {
            tracker = Track(timeout, refreshEvent, stateMachine.MoveToCandidateState, trackerCancellation.Token);
            return this;
        }

        internal override Task StopAsync()
        {
            trackerCancellation.Cancel();
            return tracker.OnCompleted();
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

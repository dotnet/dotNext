using System;
using System.Threading;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal sealed class FollowerState : RaftState
    {
        private readonly AutoResetEvent refreshEvent;
        private readonly RegisteredWaitHandle timerHandle;

        internal FollowerState(IRaftStateMachine stateMachine, in TimeSpan timeout)
            : base(stateMachine)
        {
            timerHandle = ThreadPool.RegisterWaitForSingleObject(refreshEvent = new AutoResetEvent(false), TimerEvent,
                null, timeout, false);
        }

        private void TimerEvent(object state, bool timedOut)
        {
            if (IsDisposed || !timedOut)
                return;
            timerHandle.Unregister(null);
            stateMachine.MoveToCandidateState();
        }

        internal void Refresh()
        {
            stateMachine.Logger.TimeoutReset();
            refreshEvent.Set();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timerHandle.Unregister(null);
                refreshEvent.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

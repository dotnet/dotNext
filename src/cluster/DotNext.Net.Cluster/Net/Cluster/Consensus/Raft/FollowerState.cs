using System;
using System.Threading;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal sealed class FollowerState : RaftState
    {
        private readonly AutoResetEvent refreshEvent;
        private volatile RegisteredWaitHandle timerHandle;

        internal FollowerState(IRaftStateMachine stateMachine)
            : base(stateMachine)
        {
            refreshEvent = new AutoResetEvent(false);
        }

        private void TimerEvent(object state, bool timedOut)
        {
            if (IsDisposed || !timedOut)
                return;
            timerHandle.Unregister(null);
            stateMachine.MoveToCandidateState();
        }

        internal FollowerState StartServing(int timeout)
        {
            refreshEvent.Reset();
            timerHandle = ThreadPool.RegisterWaitForSingleObject(refreshEvent, TimerEvent,
                null, timeout, false);
            return this;
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
                timerHandle?.Unregister(null);
                refreshEvent.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

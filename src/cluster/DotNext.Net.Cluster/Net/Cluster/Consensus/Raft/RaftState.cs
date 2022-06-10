using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft;

internal abstract class RaftState : Disposable, IAsyncDisposable
{
    private readonly IRaftStateMachine stateMachine;

    private protected RaftState(IRaftStateMachine stateMachine) => this.stateMachine = stateMachine;

    private protected ILogger Logger => stateMachine.Logger;

    private protected IReadOnlyCollection<IRaftClusterMember> Members => stateMachine.Members;

    private protected void UpdateLeaderStickiness() => stateMachine.UpdateLeaderStickiness();

    private protected void MoveToCandidateState()
        => ThreadPool.UnsafeQueueUserWorkItem(new TransitionToCandidateState(this), preferLocal: true);

    private protected void MoveToLeaderState(IRaftClusterMember member)
        => ThreadPool.UnsafeQueueUserWorkItem(new TransitionToLeaderState(this, member), preferLocal: true);

    private protected void MoveToFollowerState(bool randomizeTimeout, long? newTerm = null)
        => ThreadPool.UnsafeQueueUserWorkItem(new TransitionToFollowerState(this, randomizeTimeout, newTerm), preferLocal: true);

    public new ValueTask DisposeAsync() => base.DisposeAsync();

    private abstract class StateTransition : WeakReference
    {
        private protected StateTransition(RaftState currentState)
            : base(currentState)
        {
        }
    }

    private sealed class TransitionToCandidateState : StateTransition, IThreadPoolWorkItem
    {
        internal TransitionToCandidateState(RaftState currentState)
            : base(currentState)
        {
        }

        void IThreadPoolWorkItem.Execute()
        {
            if (Target is RaftState currentState)
                currentState.stateMachine.MoveToCandidateState(this);
        }
    }

    private sealed class TransitionToFollowerState : StateTransition, IThreadPoolWorkItem
    {
        private readonly bool randomizeTimeout;
        private readonly long? newTerm;

        internal TransitionToFollowerState(RaftState currentState, bool randomizeTimeout, long? newTerm)
            : base(currentState)
        {
            this.randomizeTimeout = randomizeTimeout;
            this.newTerm = newTerm;
        }

        void IThreadPoolWorkItem.Execute()
        {
            if (Target is RaftState currentState)
                currentState.stateMachine.MoveToFollowerState(this, randomizeTimeout, newTerm);
        }
    }

    private sealed class TransitionToLeaderState : StateTransition, IThreadPoolWorkItem
    {
        private readonly IRaftClusterMember leader;

        internal TransitionToLeaderState(RaftState currentState, IRaftClusterMember leader)
            : base(currentState)
            => this.leader = leader;

        void IThreadPoolWorkItem.Execute()
        {
            if (Target is RaftState currentState)
                currentState.stateMachine.MoveToLeaderState(this, leader);
        }
    }
}
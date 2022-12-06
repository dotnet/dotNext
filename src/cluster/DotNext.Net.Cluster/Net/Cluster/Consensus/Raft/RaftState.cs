using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft;

internal abstract class RaftState<TMember> : Disposable, IAsyncDisposable
    where TMember : class, IRaftClusterMember
{
    private readonly IRaftStateMachine<TMember> stateMachine;

    private protected RaftState(IRaftStateMachine<TMember> stateMachine) => this.stateMachine = stateMachine;

    private protected ILogger Logger => stateMachine.Logger;

    private protected IReadOnlyCollection<TMember> Members => stateMachine.Members;

    private protected void UpdateLeaderStickiness() => stateMachine.UpdateLeaderStickiness();

    private protected void MoveToCandidateState()
        => ThreadPool.UnsafeQueueUserWorkItem(new TransitionToCandidateState(this), preferLocal: true);

    private protected void MoveToLeaderState(TMember member)
        => ThreadPool.UnsafeQueueUserWorkItem(new TransitionToLeaderState(this, member), preferLocal: true);

    private protected void MoveToFollowerState(bool randomizeTimeout, long? newTerm = null)
        => ThreadPool.UnsafeQueueUserWorkItem(new TransitionToFollowerState(this, randomizeTimeout, newTerm), preferLocal: true);

    private protected void UnavailableMemberDetected(TMember member, CancellationToken token)
        => ThreadPool.UnsafeQueueUserWorkItem(new UnavailableMemberNotification(this, member, token), preferLocal: false);

    public new ValueTask DisposeAsync() => base.DisposeAsync();

    private abstract class StateTransition : WeakReference
    {
        private protected StateTransition(RaftState<TMember> currentState)
            : base(currentState)
        {
        }
    }

    private sealed class TransitionToCandidateState : StateTransition, IThreadPoolWorkItem
    {
        internal TransitionToCandidateState(RaftState<TMember> currentState)
            : base(currentState)
        {
        }

        void IThreadPoolWorkItem.Execute()
        {
            if (Target is RaftState<TMember> currentState)
                currentState.stateMachine.MoveToCandidateState(this);
        }
    }

    private sealed class TransitionToFollowerState : StateTransition, IThreadPoolWorkItem
    {
        private readonly bool randomizeTimeout;
        private readonly long? newTerm;

        internal TransitionToFollowerState(RaftState<TMember> currentState, bool randomizeTimeout, long? newTerm)
            : base(currentState)
        {
            this.randomizeTimeout = randomizeTimeout;
            this.newTerm = newTerm;
        }

        void IThreadPoolWorkItem.Execute()
        {
            if (Target is RaftState<TMember> currentState)
                currentState.stateMachine.MoveToFollowerState(this, randomizeTimeout, newTerm);
        }
    }

    private sealed class TransitionToLeaderState : StateTransition, IThreadPoolWorkItem
    {
        private readonly TMember leader;

        internal TransitionToLeaderState(RaftState<TMember> currentState, TMember leader)
            : base(currentState)
            => this.leader = leader;

        void IThreadPoolWorkItem.Execute()
        {
            if (Target is RaftState<TMember> currentState)
                currentState.stateMachine.MoveToLeaderState(this, leader);
        }
    }

    private sealed class UnavailableMemberNotification : StateTransition, IThreadPoolWorkItem
    {
        private readonly TMember member;
        private readonly CancellationToken token;

        internal UnavailableMemberNotification(RaftState<TMember> currentState, TMember member, CancellationToken token)
            : base(currentState)
        {
            this.member = member;
            this.token = token;
        }

        void IThreadPoolWorkItem.Execute()
        {
            if (Target is RaftState<TMember> currentState)
                currentState.stateMachine.UnavailableMemberDetected(this, member, token);
        }
    }
}
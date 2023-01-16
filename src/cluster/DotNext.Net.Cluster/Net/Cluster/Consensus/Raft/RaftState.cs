using System.Runtime.InteropServices;
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

    // holds weak reference to the state that was an initiator of the work item
    private abstract class StateTransitionWorkItem : IRaftStateMachine.IWeakCallerStateIdentity, IThreadPoolWorkItem
    {
        private const nint ZeroHandle = 0;
        private volatile nint handle;

        private protected StateTransitionWorkItem(RaftState<TMember> state)
            => handle = (nint)GCHandle.Alloc(state, GCHandleType.Weak);

        private RaftState<TMember>? Target
        {
            get
            {
                var handle = this.handle;

                var target = handle != ZeroHandle
                    ? GCHandle.FromIntPtr(handle).Target as RaftState<TMember>
                    : null;

                GC.KeepAlive(this); // to prevent finalization of the work item
                return target;
            }
        }

        public bool IsValid(object? state) => ReferenceEquals(Target, state);

        private void ClearCore()
        {
            var handle = Interlocked.Exchange(ref this.handle, ZeroHandle);

            if (handle != ZeroHandle)
                GCHandle.FromIntPtr(handle).Free();
        }

        public void Clear()
        {
            ClearCore();
            GC.SuppressFinalize(this);
        }

        private protected abstract void Execute(RaftState<TMember> currentState);

        void IThreadPoolWorkItem.Execute()
        {
            var currentState = Target;

            // reference is dead, release GC handle ASAP
            if (currentState is null)
                Clear();
            else
                Execute(currentState);
        }

        // Likely never be executed because all consumers call Clear() explicitly.
        // However, we want to prevent handle leaks in case of bugs
        ~StateTransitionWorkItem() => ClearCore();
    }

    private sealed class TransitionToCandidateState : StateTransitionWorkItem
    {
        internal TransitionToCandidateState(RaftState<TMember> currentState)
            : base(currentState)
        {
        }

        private protected override void Execute(RaftState<TMember> currentState)
            => currentState.stateMachine.MoveToCandidateState(this);
    }

    private sealed class TransitionToFollowerState : StateTransitionWorkItem
    {
        private readonly bool randomizeTimeout;
        private readonly long? newTerm;

        internal TransitionToFollowerState(RaftState<TMember> currentState, bool randomizeTimeout, long? newTerm)
            : base(currentState)
        {
            this.randomizeTimeout = randomizeTimeout;
            this.newTerm = newTerm;
        }

        private protected override void Execute(RaftState<TMember> currentState)
            => currentState.stateMachine.MoveToFollowerState(this, randomizeTimeout, newTerm);
    }

    private sealed class TransitionToLeaderState : StateTransitionWorkItem
    {
        private readonly TMember leader;

        internal TransitionToLeaderState(RaftState<TMember> currentState, TMember leader)
            : base(currentState)
            => this.leader = leader;

        private protected override void Execute(RaftState<TMember> currentState)
            => currentState.stateMachine.MoveToLeaderState(this, leader);
    }

    private sealed class UnavailableMemberNotification : StateTransitionWorkItem
    {
        private readonly TMember member;
        private readonly CancellationToken token;

        internal UnavailableMemberNotification(RaftState<TMember> currentState, TMember member, CancellationToken token)
            : base(currentState)
        {
            this.member = member;
            this.token = token;
        }

        private protected override void Execute(RaftState<TMember> currentState)
            => currentState.stateMachine.UnavailableMemberDetected(this, member, token);
    }
}
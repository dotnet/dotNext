using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Diagnostics;

internal abstract class RaftState<TMember> : Disposable, IAsyncDisposable
    where TMember : class, IRaftClusterMember
{
    private readonly IRaftStateMachine<TMember> stateMachine;

    private protected RaftState(IRaftStateMachine<TMember> stateMachine) => this.stateMachine = stateMachine;

    private protected ref readonly TagList MeasurementTags => ref stateMachine.MeasurementTags;

    private protected ILogger Logger => stateMachine.Logger;

    private protected IReadOnlyCollection<TMember> Members => stateMachine.Members;

    private protected void UpdateLeaderStickiness(Timestamp refreshedAt) => stateMachine.UpdateLeaderStickiness(refreshedAt);

    private protected void MoveToCandidateState()
        => ThreadPool.UnsafeQueueUserWorkItem(new TransitionToCandidateState(this), preferLocal: true);

    private protected void MoveToLeaderState(TMember member, long writeBarrier)
        => ThreadPool.UnsafeQueueUserWorkItem(new TransitionToLeaderState(this, member, writeBarrier), preferLocal: true);

    private protected void MoveToFollowerState(bool randomizeTimeout, long? newTerm = null)
        => ThreadPool.UnsafeQueueUserWorkItem(new TransitionToFollowerState(this, randomizeTimeout, newTerm), preferLocal: true);

    private protected void UnavailableMemberDetected(TMember member, long currentTerm, CancellationToken token)
        => ThreadPool.UnsafeQueueUserWorkItem(new UnavailableMemberNotification(this, member, currentTerm, token), preferLocal: false);

    private protected void IncomingHeartbeatTimedOut()
        => ThreadPool.UnsafeQueueUserWorkItem(new IncomingHeartbeatTimedOutNotification(this), preferLocal: true);

    public new ValueTask DisposeAsync() => base.DisposeAsync();

    // holds weak reference to the state that was an initiator of the work item
    private abstract class StateTransitionWorkItem : IRaftStateMachine.IWeakCallerStateIdentity, IThreadPoolWorkItem
    {
        private const nint ZeroHandle = 0;
        private volatile nint handle;

        private protected StateTransitionWorkItem(RaftState<TMember> state)
            => handle = WeakGCHandle<RaftState<TMember>>.ToIntPtr(new(state));

        private RaftState<TMember>? Target
        {
            get
            {
                var weakHandle = WeakGCHandle<RaftState<TMember>>.FromIntPtr(handle);
                if (!weakHandle.IsAllocated || !weakHandle.TryGetTarget(out var target))
                    target = null;

                return target;
            }
        }

        public bool IsValid([NotNullWhen(true)] object? state)
            => Target is { } target && ReferenceEquals(target, state);

        private void ClearCore()
        {
            if (WeakGCHandle<RaftState<TMember>>
                    .FromIntPtr(Interlocked.Exchange(ref handle, ZeroHandle)) is { IsAllocated: true } weakHandle)
                weakHandle.Dispose();
        }

        public void Clear()
        {
            ClearCore();
            GC.SuppressFinalize(this);
        }

        private protected abstract void Execute(IRaftStateMachine<TMember> stateMachine);

        void IThreadPoolWorkItem.Execute()
        {
            // reference is dead, release GC handle ASAP
            if (Target?.stateMachine is { } stateMachine)
                Execute(stateMachine);
            else
                Clear();
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

        private protected override void Execute(IRaftStateMachine<TMember> stateMachine)
            => _ = stateMachine.MoveToCandidateState(this);
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

        private protected override void Execute(IRaftStateMachine<TMember> stateMachine)
            => _ = stateMachine.MoveToFollowerState(this, randomizeTimeout, newTerm);
    }

    private sealed class TransitionToLeaderState : StateTransitionWorkItem
    {
        private readonly TMember leader;
        private readonly long writeBarrier;

        internal TransitionToLeaderState(RaftState<TMember> currentState, TMember leader, long writeBarrier)
            : base(currentState)
        {
            this.leader = leader;
            this.writeBarrier = writeBarrier;
        }

        private protected override void Execute(IRaftStateMachine<TMember> stateMachine)
            => _ = stateMachine.MoveToLeaderState(this, leader, writeBarrier);
    }

    private sealed class UnavailableMemberNotification : StateTransitionWorkItem
    {
        private readonly TMember member;
        private readonly CancellationToken token;
        private readonly long currentTerm;

        internal UnavailableMemberNotification(RaftState<TMember> currentState, TMember member, long currentTerm, CancellationToken token)
            : base(currentState)
        {
            this.member = member;
            this.token = token;
            this.currentTerm = currentTerm;
        }

        private protected override void Execute(IRaftStateMachine<TMember> stateMachine)
            => _ = stateMachine.UnavailableMemberDetected(this, member, currentTerm, token);
    }

    private sealed class IncomingHeartbeatTimedOutNotification(RaftState<TMember> currentState) : StateTransitionWorkItem(currentState)
    {
        private protected override void Execute(IRaftStateMachine<TMember> stateMachine)
            => _ = stateMachine.IncomingHeartbeatTimedOut(this);
    }
}
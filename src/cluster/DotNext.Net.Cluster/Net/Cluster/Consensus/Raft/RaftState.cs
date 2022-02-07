using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft;

using ThreadPoolWorkItemFactory = Threading.ThreadPoolWorkItemFactory;

internal abstract class RaftState : Disposable
{
    private readonly IRaftStateMachine stateMachine;

    private protected RaftState(IRaftStateMachine stateMachine) => this.stateMachine = stateMachine;

    private protected ILogger Logger => stateMachine.Logger;

    private protected IReadOnlyCollection<IRaftClusterMember> Members => stateMachine.Members;

    internal abstract Task StopAsync();

    private protected void UpdateLeaderStickiness() => stateMachine.UpdateLeaderStickiness();

    private protected unsafe void MoveToCandidateState()
    {
        ThreadPool.UnsafeQueueUserWorkItem(ThreadPoolWorkItemFactory.Create(&MoveToCandidateState, stateMachine), preferLocal: true);

        static void MoveToCandidateState(IRaftStateMachine stateMachine) => stateMachine.MoveToCandidateState();
    }

    private protected unsafe void MoveToLeaderState(IRaftClusterMember member)
    {
        ThreadPool.UnsafeQueueUserWorkItem(ThreadPoolWorkItemFactory.Create(&MoveToLeaderState, stateMachine, member), preferLocal: true);

        static void MoveToLeaderState(IRaftStateMachine stateMachine, IRaftClusterMember member)
            => stateMachine.MoveToLeaderState(member);
    }

    private protected unsafe void MoveToFollowerState(bool randomizeTimeout, long? newTerm = null)
    {
        ThreadPool.UnsafeQueueUserWorkItem(ThreadPoolWorkItemFactory.Create(&MoveToFollowerState, stateMachine, randomizeTimeout, newTerm), preferLocal: true);

        static void MoveToFollowerState(IRaftStateMachine stateMachine, bool randomizeTimeout, long? newTerm)
            => stateMachine.MoveToFollowerState(randomizeTimeout, newTerm);
    }
}

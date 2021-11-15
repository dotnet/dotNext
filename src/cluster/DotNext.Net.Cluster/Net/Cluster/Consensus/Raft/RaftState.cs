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

    private protected unsafe IThreadPoolWorkItem MoveToCandidateStateWorkItem()
    {
        return ThreadPoolWorkItemFactory.Create(&MoveToCandidateState, stateMachine);

        static void MoveToCandidateState(IRaftStateMachine stateMachine) => stateMachine.MoveToCandidateState();
    }

    private protected unsafe IThreadPoolWorkItem MoveToLeaderStateWorkItem(IRaftClusterMember member)
    {
        return ThreadPoolWorkItemFactory.Create(&MoveToLeaderState, stateMachine, member);

        static void MoveToLeaderState(IRaftStateMachine stateMachine, IRaftClusterMember member)
            => stateMachine.MoveToLeaderState(member);
    }

    private protected unsafe IThreadPoolWorkItem MoveToFollowerStateWorkItem(bool randomizeTimeout, long? newTerm = null)
    {
        return ThreadPoolWorkItemFactory.Create(&MoveToFollowerState, stateMachine, randomizeTimeout, newTerm);

        static void MoveToFollowerState(IRaftStateMachine stateMachine, bool randomizeTimeout, long? newTerm)
            => stateMachine.MoveToFollowerState(randomizeTimeout, newTerm);
    }
}

using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft;

internal interface IRaftStateMachine
{
    ILogger Logger { get; }

    IReadOnlyCollection<IRaftClusterMember> Members { get; }

    void UpdateLeaderStickiness();
}

internal interface IRaftStateMachine<TMember> : IRaftStateMachine
    where TMember : class, IRaftClusterMember
{
    new IReadOnlyCollection<TMember> Members { get; }

    IReadOnlyCollection<IRaftClusterMember> IRaftStateMachine.Members => Members;

    void MoveToFollowerState(WeakReference callerState, bool randomizeTimeout, long? newTerm);

    void MoveToCandidateState(WeakReference callerState);

    void MoveToLeaderState(WeakReference callerState, TMember leader);

    void UnavailableMemberDetected(WeakReference callerState, TMember member, CancellationToken token);
}
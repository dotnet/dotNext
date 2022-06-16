using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft;

internal interface IRaftStateMachine
{
    ILogger Logger { get; }

    IReadOnlyCollection<IRaftClusterMember> Members { get; }

    void UpdateLeaderStickiness();

    void MoveToFollowerState(WeakReference callerState, bool randomizeTimeout, long? newTerm);

    void MoveToCandidateState(WeakReference callerState);

    void MoveToLeaderState(WeakReference callerState, IRaftClusterMember leader);
}
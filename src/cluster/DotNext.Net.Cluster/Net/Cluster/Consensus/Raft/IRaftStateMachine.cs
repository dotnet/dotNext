using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft;

internal interface IRaftStateMachine
{
    ILogger Logger { get; }

    IReadOnlyCollection<IRaftClusterMember> Members { get; }

    void MoveToFollowerState(bool randomizeTimeout, long? newTerm);

    void MoveToCandidateState();

    void MoveToLeaderState(IRaftClusterMember leader);
}
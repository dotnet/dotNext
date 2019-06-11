using System.Collections.Generic;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal interface IRaftStateMachine
    {
        IEnumerable<IRaftClusterMember> Members { get; }
        void MoveToFollowerState(bool randomizeTimeout);

        void MoveToCandidateState();

        void MoveToLeaderState(IRaftClusterMember leader);
    }
}

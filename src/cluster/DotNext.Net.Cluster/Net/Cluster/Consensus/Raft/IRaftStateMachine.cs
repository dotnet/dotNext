using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal interface IRaftStateMachine
    {
        ILogger Logger { get; }

        bool AbsoluteMajority { get; }

        IEnumerable<IRaftClusterMember> Members { get; }
        void MoveToFollowerState(bool randomizeTimeout);

        void MoveToCandidateState();

        void MoveToLeaderState(IRaftClusterMember leader);
    }
}

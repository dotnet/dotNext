using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal interface IRaftStateMachine
    {
        ILogger Logger { get; }

        IEnumerable<IRaftClusterMember> Members { get; }
        void MoveToFollowerState(bool randomizeTimeout, long? newTerm = null);

        void MoveToCandidateState();

        void MoveToLeaderState(IRaftClusterMember leader);

        Task NotifyBroadcastFinished(CancellationToken token);
    }
}

using System.Collections.Generic;
using System.Net;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal interface IHostingContext
    {
        bool IsLeader(IRaftClusterMember member);

        IPEndPoint LocalEndpoint { get; }

        void MemberStatusChanged(IRaftClusterMember member, ClusterMemberStatus previousStatus, ClusterMemberStatus newStatus);

        IReadOnlyDictionary<string, string> Metadata { get; }
    }
}

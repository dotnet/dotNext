using System;
using System.Collections.Generic;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal interface ISite
    {
        ref readonly Guid LocalMemberId { get; }

        IReadOnlyDictionary<string, string> LocalMemberMetadata { get; }

        bool IsLeader(IRaftClusterMember member);

        void MemberStatusChanged(IRaftClusterMember member, ClusterMemberStatus previousStatus, ClusterMemberStatus newStatus);
    }
}

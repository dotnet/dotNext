using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using ILogEntry = Replication.ILogEntry<LogEntryId>;

    internal interface IHostingContext
    {
        bool IsLeader(IRaftClusterMember member);

        IPEndPoint LocalEndpoint { get; }

        void MemberStatusChanged(IRaftClusterMember member, ClusterMemberStatus previousStatus, ClusterMemberStatus newStatus);

        IReadOnlyDictionary<string, string> Metadata { get; }

        Task<bool> LocalCommitAsync(ILogEntry entry);
    }
}

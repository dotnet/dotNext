using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using ILogEntry = Replication.ILogEntry<LogEntryId>;

    internal interface IHostingContext
    {
        HttpMessageHandler CreateHttpHandler();

        bool IsLeader(IRaftClusterMember member);

        ILogger Logger { get; }

        IPEndPoint LocalEndpoint { get; }

        void MemberStatusChanged(IRaftClusterMember member, ClusterMemberStatus previousStatus, ClusterMemberStatus newStatus);

        IReadOnlyDictionary<string, string> Metadata { get; }

        Task<bool> LocalCommitAsync(ILogEntry entry);
    }
}

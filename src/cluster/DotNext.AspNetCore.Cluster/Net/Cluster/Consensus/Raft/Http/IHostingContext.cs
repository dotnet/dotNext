using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal interface IHostingContext
    {
        HttpMessageHandler CreateHttpHandler();

        bool IsLeader(IRaftClusterMember member);

        ILogger Logger { get; }

        IPEndPoint LocalEndpoint { get; }

        IReadOnlyDictionary<string, string> Metadata { get; }
    }
}

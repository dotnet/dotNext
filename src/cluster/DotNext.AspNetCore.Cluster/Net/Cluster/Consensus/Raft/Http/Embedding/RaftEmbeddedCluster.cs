using Microsoft.AspNetCore.Builder.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Embedding
{
    internal sealed class RaftEmbeddedCluster : RaftHttpCluster
    {
        private readonly PathString protocolPath;
        
        public RaftEmbeddedCluster(IServiceProvider services)
            : base(services, out var members)
        {
            var config = services.GetRequiredService<IOptions<RaftEmbeddedClusterMemberConfiguration>>().Value;
            protocolPath = config.ResourcePath;
            foreach (var memberUri in config.Members)
                members.Add(CreateMember(memberUri));
        }

        private protected override RaftClusterMember CreateMember(Uri address)
            => new RaftClusterMember(this, address, new Uri(protocolPath.Value, UriKind.Relative)) { Timeout = RequestTimeout };

        internal RequestDelegate CreateConsensusProtocolHandler(RequestDelegate next)
            => new MapMiddleware(next, new MapOptions { PathMatch = protocolPath, Branch = ProcessRequest }).Invoke;

    }
}

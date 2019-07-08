using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Embedding
{
    internal sealed class RaftEmbeddedCluster : RaftHttpCluster
    {
        internal readonly PathString ProtocolPath;
        
        public RaftEmbeddedCluster(IServiceProvider services)
            : base(services, out var members)
        {
            var config = services.GetRequiredService<IOptions<RaftEmbeddedClusterMemberConfiguration>>().Value;
            ProtocolPath = config.ResourcePath;
            foreach (var memberUri in config.Members)
                members.Add(CreateMember(memberUri));
        }

        private protected override RaftClusterMember CreateMember(Uri address)
        {
            var member = new RaftClusterMember(this, address, new Uri(ProtocolPath.Value, UriKind.Relative)) { Timeout = RequestTimeout };
            member.DefaultRequestHeaders.ConnectionClose = OpenConnectionForEachRequest;
            return member;
        }
    }
}

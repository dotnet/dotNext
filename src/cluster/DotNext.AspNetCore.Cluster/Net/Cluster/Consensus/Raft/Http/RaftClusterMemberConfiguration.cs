using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class RaftClusterMemberConfiguration : ClusterMemberConfiguration, IRaftClusterMemberFactory
    {
        private const string UserAgent = "Raft.NET";

        public RaftClusterMemberConfiguration()
        {
            ResourcePath = new Uri("/coordination", UriKind.Relative);
        }

        /// <summary>
        /// Gets collection of members.
        /// </summary>
        public ISet<Uri> Members { get; } = new HashSet<Uri>();

        public Uri ResourcePath { get; set; }

        IReadOnlyCollection<IRaftClusterMember> IRaftClusterMemberFactory.CreateMembers()
        {
            var builder = new ReadOnlyCollectionBuilder<IRaftClusterMember>();
            foreach (var member in Members)
            {
                var client = new RaftClusterMember((IRaftLocalMember) localMember, member, ResourcePath);
                client.DefaultRequestHeaders.ConnectionClose = true;    //to avoid network storm
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, GetType().Assembly.GetName().Version.ToString()));
                builder.Add(client);
            }

            return builder.ToReadOnlyCollection();
        }
    }
}

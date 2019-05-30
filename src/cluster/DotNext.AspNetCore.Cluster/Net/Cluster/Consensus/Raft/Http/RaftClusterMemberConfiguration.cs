using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class RaftClusterMemberConfiguration : ClusterMemberConfiguration, IRaftClusterMemberFactory
    {
        public RaftClusterMemberConfiguration()
        {
            ResourcePath = new Uri("/coordination", UriKind.Relative);
        }

        /// <summary>
        /// Gets collection of members.
        /// </summary>
        public ISet<Uri> Members { get; } = new HashSet<Uri>();

        /// <summary>
        /// Gets or sets value indicating that TCP connection can be reused
        /// for multiple HTTP requests.
        /// </summary>
        public bool KeepAlive { get; set; }

        public Uri ResourcePath { get; set; }

        IReadOnlyCollection<IRaftClusterMember> IRaftClusterMemberFactory.CreateMembers(
            IClusterMemberIdentity localMember)
        {
            Debug.Assert(localMember is IRaftLocalMember);
            var builder = new ReadOnlyCollectionBuilder<IRaftClusterMember>();
            foreach (var member in Members)
            {
                var client = new RaftClusterMember((IRaftLocalMember) localMember, member, ResourcePath);
                client.DefaultRequestHeaders.ConnectionClose = !KeepAlive;
                builder.Add(client);
            }

            return builder.ToReadOnlyCollection();
        }
    }
}

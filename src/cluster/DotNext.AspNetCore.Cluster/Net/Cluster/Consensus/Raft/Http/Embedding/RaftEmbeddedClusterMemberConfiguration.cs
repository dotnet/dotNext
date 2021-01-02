using Microsoft.AspNetCore.Http;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Embedding
{
    using ComponentModel;

    internal sealed class RaftEmbeddedClusterMemberConfiguration : HttpClusterMemberConfiguration
    {
        internal const string DefaultResourcePath = "/cluster-consensus/raft";

        static RaftEmbeddedClusterMemberConfiguration() => PathStringConverter.Register();

        /// <summary>
        /// Gets or sets HTTP resource path used to capture
        /// consensus protocol messages.
        /// </summary>
        public PathString ResourcePath { get; set; } = new PathString(DefaultResourcePath);
    }
}

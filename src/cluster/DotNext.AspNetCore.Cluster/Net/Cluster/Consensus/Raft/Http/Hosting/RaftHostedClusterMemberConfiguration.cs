namespace DotNext.Net.Cluster.Consensus.Raft.Http.Hosting
{
    internal sealed class RaftHostedClusterMemberConfiguration : RaftClusterMemberConfiguration
    {
        private const int DefaultPort = 32999;

        public int Port { get; set; } = DefaultPort;
    }
}

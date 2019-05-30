namespace DotNext.Net.Cluster.Consensus.Raft
{
    public interface IRaftCluster : ICluster
    {
        long Term { get; }

        bool IsLeader(IRaftClusterMember member);
    }
}
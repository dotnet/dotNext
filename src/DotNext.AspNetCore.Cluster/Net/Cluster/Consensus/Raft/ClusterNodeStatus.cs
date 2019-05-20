namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal enum ClusterNodeStatus
    {
        Follower = 0,
        Candidate,
        Leader
    }
}

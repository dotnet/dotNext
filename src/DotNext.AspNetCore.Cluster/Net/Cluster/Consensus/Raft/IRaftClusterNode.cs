namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal interface IRaftClusterNode : IClusterNode, IRaftClusterMember
    {
        ClusterNodeStatus NodeStatus { get; }
    }
}

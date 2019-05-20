namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal interface IRaftClusterNode : IClusterNode
    {
        ClusterNodeStatus NodeStatus { get; }
    }
}

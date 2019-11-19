namespace DotNext.Net.Cluster
{
    /// <summary>
    /// Represents handler of the event occurred when a new leader in the cluster has been elected.
    /// </summary>
    /// <param name="cluster">The cluster of nodes.</param>
    /// <param name="leader">A new elected cluster leader; or <see langword="null"/> if there are no cluster-wide consensus.</param>
    public delegate void ClusterLeaderChangedEventHandler(ICluster cluster, IClusterMember? leader);
}

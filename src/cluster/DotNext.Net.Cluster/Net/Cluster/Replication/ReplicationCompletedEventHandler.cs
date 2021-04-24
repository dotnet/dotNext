namespace DotNext.Net.Cluster.Replication
{
    /// <summary>
    /// Represents the handler of the event raised when the local node has been replicated with another
    /// node in the cluster.
    /// </summary>
    /// <param name="cluster">The cluster of nodes.</param>
    /// <param name="source">The source of replication.</param>
    public delegate void ReplicationCompletedEventHandler(ICluster cluster, IClusterMember source);
}
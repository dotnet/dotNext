namespace DotNext.Net.Cluster
{
    public delegate void ClusterStatusChangedEventHandler(ICluster sender, ClusterStatus oldStatus,
        ClusterStatus newStatus);
}

namespace DotNext.Net.Cluster
{
    public delegate void ClusterStatusChangedEventHandler(IClusterNode sender, ClusterStatus oldStatus,
        ClusterStatus newStatus);
}

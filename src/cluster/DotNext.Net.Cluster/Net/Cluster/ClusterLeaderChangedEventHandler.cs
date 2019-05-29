namespace DotNext.Net.Cluster
{
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// <paramref name="leader"/> can be <see langword="null"/> if cluster has no consensus.
    /// </remarks>
    /// <param name="sender"></param>
    /// <param name="leader"></param>
    public delegate void ClusterLeaderChangedEventHandler(ICluster sender, IClusterMember leader);
}

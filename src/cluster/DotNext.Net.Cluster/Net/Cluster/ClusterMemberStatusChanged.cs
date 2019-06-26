namespace DotNext.Net.Cluster
{
    /// <summary>
    /// Represents handler of the event occured when status of the cluster member has been changed.
    /// </summary>
    /// <param name="member">The cluster member which status has been changed.</param>
    /// <param name="previousStatus">The previous member status.</param>
    /// <param name="newStatus">The current member status.</param>
    public delegate void ClusterMemberStatusChanged(IClusterMember member, ClusterMemberStatus previousStatus, ClusterMemberStatus newStatus);
}

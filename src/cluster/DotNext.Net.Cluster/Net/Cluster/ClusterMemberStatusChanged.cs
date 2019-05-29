namespace DotNext.Net.Cluster
{
    public delegate void ClusterMemberStatusChanged(IClusterMember member, ClusterMemberStatus previousStatus, ClusterMemberStatus newStatus);
}

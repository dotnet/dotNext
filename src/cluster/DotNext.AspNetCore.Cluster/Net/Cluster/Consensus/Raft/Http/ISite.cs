namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal interface ISite
    {
        ILocalClusterMember LocalMember { get; }

        bool IsLeader(IRaftClusterMember member);

        void MemberStatusChanged(IRaftClusterMember member, ClusterMemberStatus previousStatus, ClusterMemberStatus newStatus);
    }
}

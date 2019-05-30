namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal interface IRaftLocalMember : IClusterMemberIdentity
    {
        long Term { get; }

        bool IsLeader(IClusterMember member);
    }
}

namespace DotNext.Net.Cluster.Consensus.Raft.Consensus;

internal interface IReplicationCommand
{
    // false to stop the replication for the member
    bool SetResult(MemberResult? result);
}
namespace DotNext.Net.Cluster.Consensus.Raft.Consensus;

using Patterns;

internal sealed class StopReplication : IReplicationCommand, ISingleton<StopReplication>
{
    public static StopReplication Instance { get; } = new();

    private StopReplication()
    {
    }

    bool IReplicationCommand.SetResult(MemberResult? result) => false;
}
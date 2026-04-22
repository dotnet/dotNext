using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.Consensus;

[StructLayout(LayoutKind.Auto)]
internal readonly record struct ReplicationResult
{
    private readonly int replicatedCount;

    public ReplicationResult(int count, bool hasConsensus)
        => replicatedCount = count | Unsafe.BitCast<bool, byte>(hasConsensus) << 31;

    public bool HasConsensus => replicatedCount >= 0;

    public int ReplicatedCount => replicatedCount & int.MaxValue;
}
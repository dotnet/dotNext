using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.ReplicationUtils;

[StructLayout(LayoutKind.Auto)]
internal readonly record struct ReplicationResult
{
    private readonly int replicatedCount;

    public ReplicationResult(int count, bool hasConsensus)
    {
        Debug.Assert(count >= 0);

        replicatedCount = count | Unsafe.BitCast<bool, byte>(hasConsensus) << 31;
    }

    public bool HasConsensus => replicatedCount >= 0;

    public void Deconstruct(out int quorum, out bool consensus)
    {
        var count = replicatedCount;
        consensus = (quorum = count & int.MaxValue) != count;
    }
}
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.ReplicationUtils;

[StructLayout(LayoutKind.Auto)]
internal struct ReplicationState(int count)
{
    private readonly int majority = (count >>> 1) + 1;
    private int replicated, committed, unavailable;

    private readonly bool IsUnavailable => unavailable >= majority;

    private readonly bool IsCommitted => committed >= majority;

    // true only if we have a majority of successful responses, but we have not enough slots to place the majority of the committed responses
    private readonly bool IsReplicated => replicated + committed >= majority && count - replicated - unavailable < majority;

    public readonly bool TryGetConsensus(out bool consensusReached)
    {
        if (IsUnavailable)
        {
            consensusReached = false;
        }
        else if (IsCommitted || IsReplicated)
        {
            consensusReached = true;
        }
        else
        {
            Unsafe.SkipInit(out consensusReached);
            return false;
        }

        return true;
    }

    public void OnReplicated() => replicated++;

    public void OnCommitted() => committed++;

    public void Unavailable() => unavailable++;
}
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.ReplicationUtils;

[StructLayout(LayoutKind.Auto)]
internal struct ReplicationState(int majority)
{
    public int Replicated = majority, Committed = majority, Available = majority;

    public readonly bool IsReplicatedOrCommitted => Replicated is 0 || Committed is 0;

    public readonly bool IsUnavailable => Available is 0;
}
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.ReplicationUtils;

[StructLayout(LayoutKind.Auto)]
internal readonly record struct MemberResult
{
    // 0 - touched
    // > 0 - replicated with the current term
    // long.MinValue - canceled
    private readonly long indexOrTerm;

    private MemberResult(long indexOrTerm) => this.indexOrTerm = indexOrTerm;

    public long? Term
    {
        get
        {
            var term = indexOrTerm & long.MaxValue;
            return indexOrTerm == term || term is 0L ? null : term;
        }
    }

    public long ReplicatedIndex => indexOrTerm > 0L ? indexOrTerm : 0L;

    public bool IsCanceled => indexOrTerm is long.MinValue;

    public static MemberResult Canceled => new(long.MinValue);

    public static MemberResult HigherTermDetected(long term)
    {
        Debug.Assert(term > 0L);

        return new(term | long.MinValue);
    }

    public static MemberResult Replicated(long index)
    {
        Debug.Assert(index >= 0L);

        return new(index);
    }

    public static MemberResult Touched => default;

    public static MemberResult? Unavailable => null;

    internal bool Apply(ref ReplicationState state)
    {
        // canceled
        if (indexOrTerm is long.MinValue)
            return false;
            
        var term = indexOrTerm & long.MaxValue;
            
        // touched
        if (term is 0L)
        {
            state.OnReplicated();
        }
        else if (indexOrTerm == term)
        {
            state.OnCommitted();
        }
        else
        {
            // higher term detected
            return false;
        }

        return true;
    }
}
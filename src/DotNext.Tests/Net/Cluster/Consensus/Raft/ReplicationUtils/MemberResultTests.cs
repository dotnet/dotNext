namespace DotNext.Net.Cluster.Consensus.Raft.ReplicationUtils;

public sealed class MemberResultTests : Test
{
    [Fact]
    public static void HigherTermResult()
    {
        var result = MemberResult.HigherTermDetected(20L);
        Equal(20L, result.Term);
        Equal(0L, result.ReplicatedIndex);
        False(result.IsCanceled);
    }

    [Fact]
    public static void CanceledResult()
    {
        var result = MemberResult.Canceled;
        Null(result.Term);
        Equal(0L, result.ReplicatedIndex);
        True(result.IsCanceled);
    }

    [Fact]
    public static void CommitIndexResult()
    {
        var result = MemberResult.Replicated(20L);
        Null(result.Term);
        Equal(20L, result.ReplicatedIndex);
        False(result.IsCanceled);
    }

    [Fact]
    public static void TouchedResult()
    {
        var result = MemberResult.Touched;
        Null(result.Term);
        Equal(0L, result.ReplicatedIndex);
        False(result.IsCanceled);
    }
}
namespace DotNext.Net.Cluster.Consensus.Raft.ReplicationUtils;

public class ReplicationBarrierTests : Test
{
    [Fact]
    public static async Task CheckMixedResponses()
    {
        var barrier = new ReplicationBarrier();
        var task = barrier.WaitAsync(7, 0L).AsTask();

        True(barrier.SetResult(MemberResult.Replicated(10)));
        True(barrier.SetResult(MemberResult.Replicated(10)));
        True(barrier.SetResult(MemberResult.Touched));
        True(barrier.SetResult(MemberResult.Touched));
        False(barrier.IsCompleted);

        True(barrier.SetResult(MemberResult.Replicated(10)));
        True(barrier.SetResult(MemberResult.Touched));
        True(barrier.SetResult(MemberResult.Touched));
        True(barrier.IsCompleted);

        Equal(new(7, true), await task.WaitAsync(TestToken));
    }

    [Fact]
    public static async Task CheckReplicatedMajority()
    {
        var barrier = new ReplicationBarrier();
        var task = barrier.WaitAsync(7, 0L).AsTask();

        True(barrier.SetResult(MemberResult.Touched));
        True(barrier.SetResult(MemberResult.Touched));
        True(barrier.SetResult(MemberResult.Touched));
        True(barrier.SetResult(MemberResult.Touched));
        True(barrier.IsCompleted);

        False(barrier.SetResult(MemberResult.Touched));

        Equal(new(4, true), await task.WaitAsync(TestToken));
    }

    [Fact]
    public static async Task CheckNoResponseMajority()
    {
        var barrier = new ReplicationBarrier();
        var task = barrier.WaitAsync(7, 0L).AsTask();

        True(barrier.SetResult(MemberResult.Unavailable));
        True(barrier.SetResult(MemberResult.Unavailable));
        True(barrier.SetResult(MemberResult.Unavailable));
        True(barrier.SetResult(MemberResult.Unavailable));
        False(barrier.SetResult(MemberResult.Unavailable));
        True(barrier.IsCompleted);

        Equal(new(4, false), await task.WaitAsync(TestToken));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    public static async Task Overflow(int expectedCount)
    {
        var barrier = new ReplicationBarrier();
        var task = barrier.WaitAsync(expectedCount, 0L).AsTask();

        for (var i = 0; i < expectedCount; i++)
        {
            barrier.SetResult(MemberResult.Touched);
        }

        var (quorum, hasConsensus) = await task.WaitAsync(TestToken);
        Equal(expectedCount / 2 + 1, quorum);
        True(hasConsensus);
    }

    [Fact]
    public static async Task ConsensusFor3()
    {
        var barrier = new ReplicationBarrier();
        var task = barrier.WaitAsync(memberCount: 3, 0L).AsTask();
        True(barrier.SetResult(MemberResult.Unavailable));
        True(barrier.SetResult(MemberResult.Touched));
        True(barrier.SetResult(MemberResult.Replicated(2L)));

        var result = await task.WaitAsync(TestToken);
        True(result.HasConsensus);
    }

    [Fact]
    public static async Task NoConsensusFor3()
    {
        var barrier = new ReplicationBarrier();
        var task = barrier.WaitAsync(memberCount: 3, 0L).AsTask();
        True(barrier.SetResult(MemberResult.Unavailable));
        True(barrier.SetResult(MemberResult.Replicated(2L)));
        True(barrier.SetResult(MemberResult.Unavailable));

        var result = await task.WaitAsync(TestToken);
        False(result.HasConsensus);
    }

    [Fact]
    public static async Task ConsensusFor2()
    {
        var barrier = new ReplicationBarrier();
        var task = barrier.WaitAsync(memberCount: 2, 0L).AsTask();
        True(barrier.SetResult(MemberResult.Touched));
        True(barrier.SetResult(MemberResult.Replicated(2L)));

        var result = await task.WaitAsync(TestToken);
        True(result.HasConsensus);
    }
}
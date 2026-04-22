namespace DotNext.Net.Cluster.Consensus.Raft.Consensus;

public class ReplicationBarrierTests : Test
{
    [Fact]
    public static async Task CheckMixedResponses()
    {
        var barrier = new ReplicationBarrier();
        var task = barrier.WaitAsync(7).AsTask();
        
        barrier.SetResult(MemberResult.Committed(10));
        barrier.SetResult(MemberResult.Committed(10));
        barrier.SetResult(MemberResult.Touched);
        barrier.SetResult(MemberResult.Touched);
        False(barrier.IsCompleted);
        
        barrier.SetResult(MemberResult.Committed(10));
        barrier.SetResult(MemberResult.Touched);
        barrier.SetResult(MemberResult.Touched);
        True(barrier.IsCompleted);

        Equal(new(7, true), await task.WaitAsync(TestToken));
    }
    
    [Fact]
    public static async Task CheckReplicatedMajority()
    {
        var barrier = new ReplicationBarrier();
        var task = barrier.WaitAsync(7).AsTask();
        
        barrier.SetResult(MemberResult.Touched);
        barrier.SetResult(MemberResult.Touched);
        barrier.SetResult(MemberResult.Touched);
        barrier.SetResult(MemberResult.Touched);
        True(barrier.IsCompleted);

        Equal(new(4, true), await task.WaitAsync(TestToken));
    }
    
    [Fact]
    public static async Task CheckNoResponseMajority()
    {
        var barrier = new ReplicationBarrier();
        var task = barrier.WaitAsync(7).AsTask();
        
        barrier.SetResult(null);
        barrier.SetResult(null);
        barrier.SetResult(null);
        barrier.SetResult(null);
        True(barrier.IsCompleted);

        Equal(new(4, false), await task.WaitAsync(TestToken));
    }
}
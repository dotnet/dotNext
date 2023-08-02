namespace DotNext.Net.Cluster.Consensus.Raft;

public sealed class TermCacheTests : Test
{
    [Fact]
    public static void AddAndGet()
    {
        var cache = new LeaderState<RaftClusterMember>.TermCache();
        cache.Add(10, 42);
        cache.Add(11, 43);
        cache.Add(9, 41);
        cache.Add(15, 50);
        False(cache.TryGet(12, out var term));
        True(cache.TryGet(9, out term));
        Equal(41, term);
        True(cache.TryGet(15, out term));
        Equal(50, term);
        True(cache.TryGet(10, out term));
        Equal(42, term);
        Equal(4, cache.ApproximatedCount);

        cache.RemovePriorTo(11);
        Equal(4, cache.ApproximatedCount);
        True(cache.TryGet(11, out term));
        Equal(43, term);
        False(cache.TryGet(10, out term));

        cache.RemovePriorTo(15);
        Equal(1, cache.ApproximatedCount);
    }
}
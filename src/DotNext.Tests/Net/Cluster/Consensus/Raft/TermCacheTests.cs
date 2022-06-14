namespace DotNext.Net.Cluster.Consensus.Raft
{
    public sealed class TermCacheTests : Test
    {
        [Fact]
        public static void AddAndGet()
        {
            var cache = new LeaderState.TermCache(10);
            cache.Add(10, 42);
            cache.Add(11, 43);
            cache.Add(9, 41);
            cache.Add(15, 50);
            False(cache.TryGetValue(12, out var term));
            True(cache.TryGetValue(9, out term));
            Equal(41, term);
            True(cache.TryGetValue(15, out term));
            Equal(50, term);
            True(cache.TryGetValue(10, out term));
            Equal(42, term);

            cache.RemoveHead(11);
            True(cache.TryGetValue(11, out term));
            Equal(43, term);
            False(cache.TryGetValue(10, out term));
        }
    }
}
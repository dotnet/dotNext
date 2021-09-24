using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    [ExcludeFromCodeCoverage]
    public sealed class ElectionTimeoutTests : Test
    {
        [Fact]
        public static void RandomTimeout()
        {
            var timeout = new ElectionTimeout { LowerValue = 10, UpperValue = 20 };
            Equal(10, timeout.LowerValue);
            Equal(20, timeout.UpperValue);
            True(timeout.RandomTimeout(Random.Shared).IsBetween(10, 20, BoundType.Closed));
        }
    }
}
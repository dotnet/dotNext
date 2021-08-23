using System;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    [ExcludeFromCodeCoverage]
    public sealed class ElectionTimeoutTests : Test
    {
        [Fact]
        public static void RandomTimeout()
        {
            var timeout = new ElectionTimeout(10, 20);
            Equal(10, timeout.LowerValue);
            Equal(20, timeout.UpperValue);
            True(timeout.RandomTimeout(new Random()).IsBetween(10, 20, BoundType.Closed));
        }

        [Fact]
        public static void UpdateBoundaries()
        {
            var timeout = new ElectionTimeout(10, 20);
            timeout.Update(null, null);
            Equal(10, timeout.LowerValue);
            Equal(20, timeout.UpperValue);
            timeout.Update(15, null);
            Equal(15, timeout.LowerValue);
            Equal(20, timeout.UpperValue);
            timeout.Update(null, 30);
            Equal(15, timeout.LowerValue);
            Equal(30, timeout.UpperValue);
            timeout.Update(50, 60);
            Equal(50, timeout.LowerValue);
            Equal(60, timeout.UpperValue);
        }
    }
}
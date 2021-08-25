using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Net.ConsistentHash
{
    [ExcludeFromCodeCoverage]
    public sealed class JumpHashTests : Test
    {
        // test data taken from https://github.com/lithammer/python-jump-consistent-hash/blob/master/tests/test_jump.py
        [Theory]
        [InlineData(1L, 1, 0)]
        [InlineData(42L, 57, 43)]
        [InlineData(0xDEAD10CCL, 1, 0)]
        [InlineData(0xDEAD10CCL, 666, 361)]
        [InlineData(256L, 1024, 520)]
        public static void CheckHash(long key, int buckets, int expected)
        {
            Equal(expected, JumpHash.GetBucket(key, buckets));
        }
    }
}
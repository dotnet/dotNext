using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Xunit;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    public sealed class RandomTests : Test
    {
        private sealed class DummyRNG : RandomNumberGenerator
        {
            private readonly byte[] number;

            internal DummyRNG(int value) => number = BitConverter.GetBytes(value);

            internal DummyRNG(long value) => number = BitConverter.GetBytes(value);

            public override void GetBytes(byte[] data) => number.CopyTo(data, 0);
        }

        [Fact]
        public static void RandomInt()
        {
            using (var rng = new DummyRNG(42))
            {
                Equal(42, rng.Next());
            }
        }

        public static IEnumerable<object[]> RandomDoubleTestData()
        {
            yield return new[] { new DummyRNG(0) };
            yield return new[] { new DummyRNG(int.MaxValue) };
            yield return new[] { new DummyRNG(int.MinValue) };
            yield return new[] { new DummyRNG(-1) };
            yield return new[] { new DummyRNG(1) };
        }

        [Theory]
        [MemberData(nameof(RandomDoubleTestData))]
        public static void RandomDouble(RandomNumberGenerator rng)
        {
            InRange(rng.NextDouble(), 0, 1 - double.Epsilon);
        }
    }
}

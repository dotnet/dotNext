using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using Xunit;

namespace DotNext
{
    public sealed class RandomTests: Assert
    {
        private sealed class DummyRNG : RandomNumberGenerator
        {
            private readonly byte[] number;

            internal DummyRNG(int value) => number = BitConverter.GetBytes(value);

            internal DummyRNG(long value) => number = BitConverter.GetBytes(value);

            public override void GetBytes(byte[] data) => number.CopyTo(data, 0);
        }

        public sealed class RandomDoubleTestData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new[] { new DummyRNG(0) };
                yield return new[] { new DummyRNG(int.MaxValue) };
                yield return new[] { new DummyRNG(int.MinValue) };
                yield return new[] { new DummyRNG(-1) };
                yield return new[] { new DummyRNG(1) };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Fact]
        public static void RandomInt()
        {
            using (var rng = new DummyRNG(42))
            {
                Equal(42, rng.Next());
            }
        }

        [Theory]
        [ClassData(typeof(RandomDoubleTestData))]
        public static void RandomDouble(RandomNumberGenerator rng)
        {
            InRange(rng.NextDouble(), 0, 1 - double.Epsilon);
        }
    }
}

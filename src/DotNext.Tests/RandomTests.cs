using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

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

            public override void GetBytes(byte[] data) => number.CopyTo(data.AsSpan());
        }

        [Fact]
        public static void RandomInt()
        {
            using var rng = new DummyRNG(42);
            Equal(42 >> 1, rng.Next()); // because Next applies >> 1 for every randomly generated 32-bit unsigned integer
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

        [Fact]
        public static void MakeRandomGuids()
        {
            var buffer = new Guid[4];

            Random.Shared.GetItems<Guid>(buffer);
            All(buffer, static v => NotEqual(Guid.Empty, v));
            Array.Clear(buffer);

            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetItems<Guid>(buffer);
            }

            All(buffer, static v => NotEqual(Guid.Empty, v));
        }
    }
}

using System.Security.Cryptography;

namespace DotNext;

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

    [Theory]
    [InlineData("abcd123456789", 6)]
    [InlineData("abcd123456789", 7)]
    [InlineData("0123456789ABCDEF", 12)] // allowedChars.Length is pow of 2
    public static void RandomString(string allowedChars, int length)
    {
        var str = Random.Shared.NextString(allowedChars, length);
        Equal(length, str.Length);
        All(str, ch => True(allowedChars.Contains(ch)));

        using var generator = RandomNumberGenerator.Create();
        str = generator.NextString(allowedChars, length);
        Equal(length, str.Length);
        All(str, ch => True(allowedChars.Contains(ch)));
    }

    [Fact]
    public static void RandomChars()
    {
        const string AllowedChars = "abcd123456789";
        var str = new char[6];

        Random.Shared.NextChars(AllowedChars, str);
        All(str, static ch => True(AllowedChars.Contains(ch)));

        using var generator = RandomNumberGenerator.Create();
        Array.Clear(str);
        generator.NextChars(AllowedChars, str);

        All(str, static ch => True(AllowedChars.Contains(ch)));
    }

    [Fact]
    public static void PeekRandomFromEmptyCollection()
    {
        False(Random.Shared.Peek(Array.Empty<int>()).HasValue);
    }

    [Fact]
    public static void PeekRandomFromSingletonCollection()
    {
        Equal(5, Random.Shared.Peek(new int[] { 5 }));
    }

    [Fact]
    public static void PeekRandomFromCollection()
    {
        IReadOnlyCollection<int> collection = new int[] { 10, 20, 30 };
        All(Enumerable.Range(0, collection.Count), i =>
        {
            True(Random.Shared.Peek(collection).Value is 10 or 20 or 30);
        });
    }

    [Fact]
    public static void ShuffleList()
    {
        var list = new List<int> { 1, 2, 3, 4, 5, 6, 7 };
        Random.Shared.Shuffle(list);
        NotEqual([1, 2, 3, 4, 5, 6, 7], list.ToArray());
    }
}
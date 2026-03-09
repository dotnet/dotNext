namespace DotNext.Buffers;

public sealed class BufferUtilsTests : Test
{
    [Fact]
    public static void ReadOnlyMemoryTrimLength()
    {
        Equal("ab", ("abcd".AsMemory() % 2).ToString());
        Equal("ab", "ab".AsMemory().TrimLength(10).ToString());
        True(ReadOnlyMemory<char>.Empty.TrimLength(10).IsEmpty);
        True("ab".AsMemory().TrimLength(0).IsEmpty);
    }

    [Fact]
    public static void MemoryTrimLength()
    {
        Equal("ab", (new[] { 'a', 'b', 'c', 'd' }.AsMemory() % 2).ToString());
        Equal("ab", new[] { 'a', 'b' }.AsMemory().TrimLength(10).ToString());
        True(Memory<char>.Empty.TrimLength(10).IsEmpty);
        True(new[] { 'a', 'b' }.AsMemory().TrimLength(0).IsEmpty);
    }
}
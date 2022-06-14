using System.Diagnostics.CodeAnalysis;

namespace DotNext.Buffers
{
    [ExcludeFromCodeCoverage]
    public sealed class BufferUtilsTests : Test
    {
        [Fact]
        public static void ReadOnlyMemoryTrimLength()
        {
            Equal("ab", "abcd".AsMemory().TrimLength(2).ToString());
            Equal("ab", "ab".AsMemory().TrimLength(10).ToString());
            True(ReadOnlyMemory<char>.Empty.TrimLength(10).IsEmpty);
            True("ab".AsMemory().TrimLength(0).IsEmpty);
        }

        [Fact]
        public static void MemoryTrimLength()
        {
            Equal("ab", new char[] { 'a', 'b', 'c', 'd' }.AsMemory().TrimLength(2).ToString());
            Equal("ab", new char[] { 'a', 'b' }.AsMemory().TrimLength(10).ToString());
            True(Memory<char>.Empty.TrimLength(10).IsEmpty);
            True(new char[] { 'a', 'b' }.AsMemory().TrimLength(0).IsEmpty);
        }
    }
}
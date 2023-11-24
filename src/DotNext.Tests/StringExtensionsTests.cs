namespace DotNext;

public sealed class StringExtensionsTests : Test
{
    [Fact]
    public static void ReverseTest()
    {
        Equal("cba", "abc".Reverse());
        Equal("", "".Reverse());
    }

    [Fact]
    public static void TrimLengthTest()
    {
        Equal("ab", "abcd".TrimLength(2));
        Null(default(string).TrimLength(2));
        Equal("ab", "ab".TrimLength(3));
        Equal(string.Empty, "ab".TrimLength(0));
        Null(default(string).TrimLength(0));
    }

    [Fact]
    public static void Substring()
    {
        Equal("abcd"[1..2], "abcd".Substring(1..2));
    }
}
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    public sealed class StringExtensionsTests : Test
    {
        [Fact]
        [Obsolete]
        public static void IfNullOrEmptyTest()
        {
            Equal("a", "".IfNullOrEmpty("a"));
            Equal("a", default(string).IfNullOrEmpty("a"));
            Equal("b", "b".IfNullOrEmpty("a"));
        }

        [Theory]
        [InlineData("abcd123456789", 6)]
        [InlineData("abcd123456789", 7)]
        [InlineData("0123456789ABCDEF", 12)] // allowedChars.Length is pow of 2
        public static void RandomStringTest(string allowedChars, int length)
        {
            var str = Random.Shared.NextString(allowedChars, length);
            Equal(length, str.Length);
            All(str, ch => True(allowedChars.Contains(ch)));

            using (var generator = RandomNumberGenerator.Create())
            {
                str = generator.NextString(allowedChars, length);
                Equal(length, str.Length);
                All(str, ch => True(allowedChars.Contains(ch)));
            }
        }

        [Fact]
        public static void RandomChars()
        {
            const string AllowedChars = "abcd123456789";
            var str = new char[6];

            Random.Shared.NextChars(AllowedChars, str);
            All(str, static ch => True(AllowedChars.Contains(ch)));

            using (var generator = RandomNumberGenerator.Create())
            {
                Array.Clear(str);
                generator.NextChars(AllowedChars, str);

                All(str, static ch => True(AllowedChars.Contains(ch)));
            }
        }

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

        [Fact]
        [Obsolete]
        public static void IsNullOrEmptyStringBuilder()
        {
            True(default(StringBuilder).IsNullOrEmpty());
            True(new StringBuilder().IsNullOrEmpty());
            False(new StringBuilder("abc").IsNullOrEmpty());
        }
    }
}

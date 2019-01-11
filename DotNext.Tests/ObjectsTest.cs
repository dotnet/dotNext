using System;
using Xunit;

namespace DotNext.Tests
{
    public sealed class ObjectsTest: Assert
    {
        [Fact]
        public void OneOfTest()
        {
            True("str".OneOf("a", "b", "str"));
        }

        [Fact]
        public void DecomposeTest()
        {
            var str = "Hello, world";
            (int index, char ch) = str.Decompose(s => s.IndexOf(','), s => s[1]);
            Equal(5, index);
            Equal('e', ch);
            str.Decompose(s => s.IndexOf(','), s => s[1], out index, out ch);
            Equal(5, index);
            Equal('e', ch);
        }
    }
}
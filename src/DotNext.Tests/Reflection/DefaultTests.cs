using System;
using Xunit;

namespace DotNext.Reflection
{
    public sealed class DefaultTests: Assert
    {
        [Fact]
        public static void RefTypeDefaultTest()
        {
            Null(Type<string>.Default);
            True(Type<string>.IsDefault(default));
            False(Type<string>.IsDefault(""));
        }

        [Fact]
        public static void StructTypeDefaultTest()
        {
            var value = default(Guid);
            True(Type<Guid>.IsDefault(value));
            value = Guid.NewGuid();
            False(Type<Guid>.IsDefault(value));
        }
    }
}
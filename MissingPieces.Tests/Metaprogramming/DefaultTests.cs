using System;
using Xunit;

namespace MissingPieces.Reflection
{
    public sealed class DefaultTests: Assert
    {
        [Fact]
        public void RefTypeDefaultTest()
        {
            Null(Type<string>.Default);
            True(Type<string>.IsDefault(default));
            False(Type<string>.IsDefault(""));
        }

        [Fact]
        public void StructTypeDefaultTest()
        {
            var value = default(Guid);
            True(Type<Guid>.IsDefault(value));
            value = Guid.NewGuid();
            False(Type<Guid>.IsDefault(value));
        }
    }
}
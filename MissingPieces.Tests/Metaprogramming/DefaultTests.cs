using System;
using Xunit;

namespace MissingPieces.Metaprogramming
{
    public sealed class DefaultTests: Assert
    {
        [Fact]
        public void RefTypeDefaultTest()
        {
            Null(Default<string>.Value);
            True(Default<string>.Is(null));
            False(Default<string>.Is(""));
        }

        [Fact]
        public void StructTypeDefaultTest()
        {
            var value = default(Guid);
            True(Default<Guid>.Is(value));
            value = Guid.NewGuid();
            False(Default<Guid>.Is(value));
        }
    }
}
using System;
using Xunit;

namespace Cheats.Tests
{
    public sealed class NumberConceptTest: Assert
    { 
        [Fact]
        public void LongTest()
        {
            var value = new Number<long>(42);
            value = value + 1;
            Equal(43L, value);
            Equal(43L.GetHashCode(), value.GetHashCode());
            Equal(43L.ToString(), value.ToString());
            True(value.Equals(43L));
            value -= 1L;
            Equal(42L, value);
            value = Number<long>.Parse("100500");
            Equal(100500L, value);
            Number<long>.TryParse("42", out value);
            Equal(42L, value);
        }

        [Fact]
        public void ByteTest()
        {
            var value = new Number<byte>(42);
            value = value + 1;
            Equal(43, value);
            value = value - 1;
            Equal(42, value);
        }
    }
}
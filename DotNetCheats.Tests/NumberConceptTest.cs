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
            value = Number<long>.Parse("100500");
            Equal(100500L, value);
            Number<long>.TryParse("42", out value);
            Equal(42L, value);
        }
    }
}
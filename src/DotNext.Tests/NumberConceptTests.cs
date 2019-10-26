using System;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext
{
    using Concept = Runtime.CompilerServices.Concept;
    using ConstraintViolationException = Reflection.ConstraintViolationException;

    [ExcludeFromCodeCoverage]
    public sealed class NumberConceptTests : Assert
    {
        [Fact]
        public static void ApplyConceptToLong()
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
            value = value * 2L;
            Equal(84L, value);
            value = value / 10;
            Equal(8L, value);
            Equal(8, (byte)value);
        }

        [Fact]
        public static void ApplyConceptToByte()
        {
            var value = new Number<byte>(42);
            value = value + 1;
            Equal(43, value);
            value = value - 1;
            Equal(42, value);
            value = value * 2;
            Equal(84, value);
        }

        [Fact]
        public static void InvalidActualType()
        {
            ThrowsAny<ConstraintViolationException>(() => Concept.Assert(typeof(Number<DateTime>)));
        }
    }
}
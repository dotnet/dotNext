using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Xunit;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    public sealed class OneDimensionalArrayTests : Assert
    {
        public sealed class Equatable
        {
            private readonly string value;

            public Equatable(string value) => this.value = value;

            public override bool Equals(object other) => other is Equatable equ && value == equ.value;

            public override int GetHashCode() => value.GetHashCode();
        }

        [Fact]
        public static void ArrayEquality2()
        {
            var array1 = new[] { new Equatable("a"), new Equatable("b") };
            var array2 = new[] { new Equatable("a"), new Equatable("b") };
            True(array1.SequenceEqual(array2));
            Equal(array1.SequenceHashCode(), array2.SequenceHashCode());
        }

        [Fact]
        public static void Insert()
        {
            int[] array = { 1, 2, 3 };
            True(new[] { 1, 4, 2, 3 }.SequenceEqual(array.Insert(4, 1)));
            True(new[] { 0, 1, 2, 3 }.SequenceEqual(array.Insert(0, 0)));
            True(new[] { 1, 2, 3, 4 }.SequenceEqual(array.Insert(4, 3)));
        }

        [Fact]
        public static void ArrayEquality()
        {
            var array1 = new[] { Guid.Empty, Guid.NewGuid(), Guid.NewGuid() };
            var array2 = new[] { Guid.Empty, array1[1], array1[2] };
            False(array1.Equals(array2));
            True(array1.SequenceEqual(array2));
            True(array1.BitwiseEquals(array2));
            array2[1] = Guid.Empty;
            False(array1.Equals(array2));
            False(array1.SequenceEqual(array2));
            False(array1.BitwiseEquals(array2));
        }

        [Fact]
        public static void BitwiseComparison()
        {
            var array1 = new[] { Guid.Empty, Guid.NewGuid(), Guid.NewGuid() };
            var array2 = new[] { Guid.Empty, array1[1], array1[2] };
            Equal(0, array1.BitwiseCompare(array2));
            array2[1] = Guid.Empty;
            True(array1.BitwiseCompare(array2) > 0);
        }

        [Fact]
        public static void Slice()
        {
            var array = new[] { 1, 2, 3, 4 };
            array = array.Slice(1, 2);
            Equal(2, array.LongLength);
            Equal(2, array[0]);
            Equal(3, array[1]);

            array = new[] { 1, 2, 3, 4 };
            array = array.Slice(0, 2);
            Equal(2, array.LongLength);
            Equal(1, array[0]);
            Equal(2, array[1]);

            array = new[] { 1, 2, 3, 4 };
            array = array.Slice(2, 3);
            Equal(2, array.LongLength);
            Equal(3, array[0]);
            Equal(4, array[1]);
        }
    }
}
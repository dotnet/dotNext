using Xunit;
using System;

namespace DotNext
{
    public sealed class OneDimensionalArrayTests: Assert
    {
        public sealed class Equatable
        {
            private readonly string value;

            public Equatable(string value) => this.value = value;

            public override bool Equals(object other) => other is Equatable equ && value == equ.value;

            public override int GetHashCode() => value.GetHashCode();
        }

        [Fact]
        public void ArrayEqualityTest2()
        {
            var array1 = new Equatable[] { new Equatable("a"), new Equatable("b") };
            var array2 = new Equatable[] { new Equatable("a"), new Equatable("b") };
            True(array1.SequenceEqual(array2));
            Equal(array1.SequenceHashCode(), array2.SequenceHashCode());
        }

        [Fact]
        public void InsertTest()
        {
            int[] array = new[]{1, 2, 3};
            True(new int[]{1, 4, 2, 3}.SequenceEqual(array.Insert(4, 1)));
            True(new int[]{0, 1, 2, 3}.SequenceEqual(array.Insert(0, 0)));
            True(new int[]{1, 2, 3, 4}.SequenceEqual(array.Insert(4, 3)));
        }

        [Fact]
        public void ArrayEqualityTest()
        {
            var array1 = new Guid[]{Guid.Empty, Guid.NewGuid(), Guid.NewGuid() };
            var array2 = new Guid[]{Guid.Empty, array1[1], array1[2] };
            False(array1.Equals(array2));
            True(array1.SequenceEqual(array2));
            True(array1.BitwiseEquals(array2));
            array2[1] = Guid.Empty;
            False(array1.Equals(array2));
            False(array1.SequenceEqual(array2));
            False(array1.BitwiseEquals(array2));
        }

        [Fact]
        public void SliceTest()
        {
            var array = new int[]{1, 2, 3, 4};
            array = array.Slice(0, 2);
            Equal(2, array.LongLength);
            Equal(1, array[0]);
            Equal(2, array[1]);
        }
    }
}
using Xunit;
using System;

namespace DotNext.Tests
{
    public sealed class ArraysTest: Assert
    {
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
    }
}
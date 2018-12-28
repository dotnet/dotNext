using Xunit;

namespace DotNetCheats.Tests
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
    }
}
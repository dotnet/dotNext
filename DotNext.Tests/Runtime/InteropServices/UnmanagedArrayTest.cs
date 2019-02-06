using System;
using Xunit;

namespace DotNext.Runtime.InteropServices
{
    public sealed class UnmanagedArrayTest: Assert
    {
        [Fact]
        public void BitwiseOperationsTest()
        {
            var array1 = new UnmanagedArray<ushort>(3);
            array1[0] = 10;
            array1[1] = 20;
            array1[2] = 30;

            var array2 = new UnmanagedArray<ushort>(3);
            array2[0] = 10;
            array2[1] = 20;
            array2[2] = 30;

            True(array1.BitwiseEquals(array2));
            Equal(0, array1.BitwiseCompare(array2));

            array2[1] = 50;
            False(array1.BitwiseEquals(array2));
            NotEqual(0, array1.BitwiseCompare(array2));

            array1.Dispose();
            array2.Dispose();
        }

        [Fact]
        public unsafe void ArrayInteropTest()
        {
            var array = new UnmanagedArray<ushort>(3);
            array[0] = 10;
            array[1] = 20;
            array[2] = 30;

            var dest = new ushort[array.Length];
            array.WriteTo(dest);
            Equal(10, dest[0]);
            Equal(20, dest[1]);
            Equal(30, dest[2]);
            dest[0] = 100;
            array.ReadFrom(dest);
            Equal(100, array[0]);
            array.Dispose();
        }

        [Fact]
        public void ReadWriteTest()
        {
            var array = new UnmanagedArray<ushort>(3);
            array[0] = 10;
            array[1] = 20;
            array[2] = 30;
            Equal(3, array.Length);
            Equal(6, array.Size);
            Equal(10, array[0]);
            Equal(20, array[1]);
            Equal(30, array[2]);
            var managedArray = System.Linq.Enumerable.ToArray(array);
            Equal(new ushort[] { 10, 20, 30 }, managedArray);
            array.Dispose();
        }
    }
}

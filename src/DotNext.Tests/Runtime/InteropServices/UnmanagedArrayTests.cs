using System;
using Xunit;

namespace DotNext.Runtime.InteropServices
{
    public sealed class UnmanagedArrayTests: Assert
    {   
        [Fact]
        public static void SliceTest()
        {
            var array = new UnmanagedArray<long>(5);
            try
            {
                array[0] = 10;
                array[1] = 20;
                array[2] = 30;
                array[3] = 40;
                array[4] = 50;
                var slice = array.Slice(1, 2);
                Equal(2, slice.Length);
                Equal(20, slice[0]);
                Equal(30, slice[1]);
                slice = array.Slice(2);
                Equal(3, slice.Length);
                Equal(30, slice[0]);
                Equal(40, slice[1]);
                Equal(50, slice[2]);
            }
            finally
            {
                array.Dispose();
            }
        }

        [Fact]
        public static void ResizeTest()
        {
            var array = new UnmanagedArray<long>(5);
            try
            {
                array[0] = 10;
                array[1] = 20;
                array[2] = 30;
                array[3] = 40;
                array[4] = 50;
                Equal(50, array[4]);
                array.Length = 2;
                Equal(2, array.Length);
                Equal(10, array[0]);
                Equal(20, array[1]);
            }
            finally
            {
                array.Dispose();
            }
        }

        [Fact]
        public static void BitwiseOperationsTest()
        {
            var array1 = new UnmanagedArray<ushort>(3);
            var array2 = new UnmanagedArray<ushort>(3);
            try
            {
                array1[0] = 10;
                array1[1] = 20;
                array1[2] = 30;


                array2[0] = 10;
                array2[1] = 20;
                array2[2] = 30;

                True(array1.BitwiseEquals(array2));
                Equal(0, array1.BitwiseCompare(array2));

                array2[1] = 50;
                False(array1.BitwiseEquals(array2));
                NotEqual(0, array1.BitwiseCompare(array2));
            }
            finally
            {
                array1.Dispose();
                array2.Dispose();
            }
        }

        [Fact]
        public static unsafe void ArrayInteropTest()
        {
            var array = new UnmanagedArray<ushort>(3);
            try
            {
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
            }
            finally
            {
                array.Dispose();
            }
        }

        [Fact]
        public static void ReadWriteTest()
        {
            var array = new UnmanagedArray<ushort>(3);
            try
            {
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
                array.Clear();
                Equal(0, array[0]);
                Equal(0, array[1]);
                Equal(0, array[2]);
            }
            finally
            {
                array.Dispose();
            }
        }

        [Fact]
        public static void EnumeratorTest()
        {
            var array = new UnmanagedArray<int>(3);
            try
            {
                array[0] = 10;
                array[1] = 20;
                array[2] = 30;
                var i = 0;
                foreach (var item in array)
                    switch (i++)
                    {
                        case 0:
                            Equal(10, item);
                            continue;
                        case 1:
                            Equal(20, item);
                            continue;
                        case 2:
                            Equal(30, item);
                            continue;
                        default:
                            throw new Exception();
                    }
            }
            finally
            {
                array.Dispose();
            }
        }

        [Fact]
        public static void SortAndSearchTest()
        {
            var array = new UnmanagedArray<long>(3);
            try
            {
                array[0] = 40;
                array[1] = 1;
                array[2] = 50;
                array.Sort();
                Equal(1, array[0]);
                Equal(40, array[1]);
                Equal(50, array[2]);
                Equal(1, array.BinarySearch(40));
            }
            finally
            {
                array.Dispose();
            }
        }

        [Fact]
        public static void FillElements()
        {
            using (var array = new UnmanagedArray<long>(3))
            {
                Equal(0, array[0]);
                array.Fill(42L);
                Equal(42L, array[0]);
            }
        }
    }
}

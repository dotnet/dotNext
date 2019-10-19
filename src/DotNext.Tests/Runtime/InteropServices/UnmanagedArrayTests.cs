using System;
using Xunit;

namespace DotNext.Runtime.InteropServices
{
    public sealed class UnmanagedArrayTests : Assert
    {
        [Fact]
        public static void SliceTest()
        {
            using (var owner = new UnmanagedMemory<long>(5))
            {
                Span<long> array = owner;
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
        }

        [Fact]
        public static void ResizeTest()
        {
            using (var owner = new UnmanagedMemory<long>(5))
            {
                Span<long> array = owner;
                array[0] = 10;
                array[1] = 20;
                array[2] = 30;
                array[3] = 40;
                array[4] = 50;
                Equal(50, array[4]);
                owner.Reallocate(2);
                array = owner;
                Equal(2, array.Length);
                Equal(10, array[0]);
                Equal(20, array[1]);
            }
        }

        [Fact]
        public static void BitwiseOperationsTest()
        {
            using (var owner1 = new UnmanagedMemory<ushort>(3))
            using (var owner2 = new UnmanagedMemory<ushort>(3))
            {
                Span<ushort> array1 = owner1;
                Span<ushort> array2 = owner2;

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
        }

        [Fact]
        public static unsafe void ArrayInteropTest()
        {
            using (var owner = new UnmanagedMemory<ushort>(3))
            {
                Span<ushort> array = owner;
                array[0] = 10;
                array[1] = 20;
                array[2] = 30;

                var dest = new ushort[array.Length];
                array.CopyTo(dest);
                Equal(10, dest[0]);
                Equal(20, dest[1]);
                Equal(30, dest[2]);
                dest[0] = 100;
                owner.ReadFrom(dest);
                Equal(100, array[0]);
            }
        }

        [Fact]
        public static void ReadWriteTest()
        {
            using (var owner = new UnmanagedMemory<ushort>(3))
            {
                var array = owner.Span;
                array[0] = 10;
                array[1] = 20;
                array[2] = 30;
                Equal(3, array.Length);
                Equal(3, owner.Length);
                Equal(6, owner.Size);
                Equal(10, array[0]);
                Equal(20, array[1]);
                Equal(30, array[2]);
                var managedArray = System.Linq.Enumerable.ToArray(owner);
                Equal(new ushort[] { 10, 20, 30 }, managedArray);
                array.Clear();
                Equal(0, array[0]);
                Equal(0, array[1]);
                Equal(0, array[2]);
            }
        }

        [Fact]
        public static void EnumeratorTest()
        {
            using (var owner = new UnmanagedMemory<int>(3))
            {
                var array = owner.Span;
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
        }
    }
}

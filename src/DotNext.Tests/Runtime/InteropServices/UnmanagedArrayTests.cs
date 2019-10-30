using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Runtime.InteropServices
{
    [ExcludeFromCodeCoverage]
    public sealed class UnmanagedArrayTests : Assert
    {
        [Fact]
        public static void SliceTest()
        {
            using (var owner = new UnmanagedMemory<long>(5))
            {
                Span<long> span = owner;
                span[0] = 10;
                span[1] = 20;
                span[2] = 30;
                span[3] = 40;
                span[4] = 50;
                var slice = span.Slice(1, 2);
                Equal(2, slice.Length);
                Equal(20, slice[0]);
                Equal(30, slice[1]);
                slice = span.Slice(2);
                Equal(3, slice.Length);
                Equal(30, slice[0]);
                Equal(40, slice[1]);
                Equal(50, slice[2]);
                var array = new long[3];
                owner.WriteTo(array);
                Equal(10, array[0]);
                Equal(20, array[1]);
                Equal(30, array[2]);
                array[0] = long.MaxValue;
                owner.ReadFrom(array);
                Equal(long.MaxValue, span[0]);
                Equal(20, span[1]);
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

        [Fact]
        public static async Task StreamInteropAsync()
        {
            using (var memory = new UnmanagedMemory<ushort>(3))
            using (var ms = new MemoryStream())
            {
                new ushort[] { 1, 2, 3 }.AsSpan().CopyTo(memory.Span);
                await memory.WriteToAsync(ms);
                Equal(6L, ms.Length);
                True(ms.TryGetBuffer(out var buffer));
                buffer.Array.ForEach((ref byte value, long index) =>
                {
                    if (value == 1)
                        value = 20;
                });
                ms.Position = 0;
                Equal(6, await memory.ReadFromAsync(ms));
                Equal(20, memory.Span[0]);
            }
        }

        [Fact]
        public static void StreamInterop()
        {
            using (var memory = new UnmanagedMemory<ushort>(3))
            using (var ms = new MemoryStream())
            {
                new ushort[] { 1, 2, 3 }.AsSpan().CopyTo(memory.Span);
                memory.WriteTo(ms);
                Equal(6L, ms.Length);
                True(ms.TryGetBuffer(out var buffer));
                buffer.Array.ForEach((ref byte value, long index) =>
                {
                    if (value == 1)
                        value = 20;
                });
                ms.Position = 0;
                Equal(6, memory.ReadFrom(ms));
                Equal(20, memory.Span[0]);
            }
        }

        [Fact]
        public static unsafe void ToStreamConversion()
        {
            using (var memory = new UnmanagedMemory<byte>(3, false))
            {
                new byte[] { 10, 20, 30 }.AsSpan().CopyTo(memory.Bytes);
                using (var stream = memory.AsStream())
                {
                    var bytes = new byte[3];
                    Equal(3, stream.Read(bytes, 0, 3));
                    Equal(10, bytes[0]);
                    Equal(20, bytes[1]);
                    Equal(30, bytes[2]);
                }
            }
        }

        [Fact]
        public static void CopyMemory()
        {
            using (var memory1 = new UnmanagedMemory<int>(3))
            {
                memory1.Span[0] = 10;
                using (var memory2 = memory1.Copy())
                {
                    Equal(10, memory2.Span[0]);
                    memory2.Span[0] = int.MaxValue;
                    Equal(int.MaxValue, memory2.Span[0]);
                    Equal(10, memory1.Span[0]);
                }
            }
        }
    }
}

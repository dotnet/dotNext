using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Buffers
{
    [ExcludeFromCodeCoverage]
    public sealed class UnmanagedMemoryPoolTests : Test
    {
        [Fact]
        public static void ReadWriteTest()
        {
            using var owner = UnmanagedMemoryPool<ushort>.Allocate(3);
            var array = owner.Span;
            array[0] = 10;
            array[1] = 20;
            array[2] = 30;
            Equal(new ushort[] { 10, 20, 30 }, owner.ToArray());
            True(owner.BitwiseCompare(new ushort[] { 10, 20, 30 }) == 0);
            True(owner.BitwiseEquals(new ushort[] { 10, 20, 30 }));
            False(owner.BitwiseEquals(new ushort[] { 10, 20, 40 }));
            True(owner.BitwiseCompare(new ushort[] { 10, 20, 40 }) < 0);
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

        [Fact]
        public static unsafe void ArrayInteropTest()
        {
            using var owner = UnmanagedMemoryPool<ushort>.Allocate(3);
            Span<ushort> array = owner.Span;
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

        [Fact]
        public static unsafe void UnmanagedMemoryAllocator()
        {
            using var owner = UnmanagedMemoryPool<ushort>.GetAllocator(false).Invoke(3, false);
            Span<ushort> array = owner.Memory.Span;
            array[0] = 10;
            array[1] = 20;
            array[2] = 30;

            var dest = new ushort[array.Length];
            array.CopyTo(dest);
            Equal(10, dest[0]);
            Equal(20, dest[1]);
            Equal(30, dest[2]);
        }

        [Fact]
        public static void BitwiseOperationsTest()
        {
            using var owner1 = UnmanagedMemoryPool<ushort>.Allocate(3);
            using var owner2 = UnmanagedMemoryPool<ushort>.Allocate(3);
            Span<ushort> array1 = owner1.Span;
            Span<ushort> array2 = owner2.Span;

            array1[0] = 10;
            array1[1] = 20;
            array1[2] = 30;


            array2[0] = 10;
            array2[1] = 20;
            array2[2] = 30;

            True(array1.BitwiseEquals(array2));
            True(owner1.BitwiseEquals(owner2));
            True(owner1.BitwiseCompare(owner2) == 0);
            True(owner1.BitwiseEquals(owner2.Pointer));
            Equal(0, array1.BitwiseCompare(array2));

            array2[1] = 50;
            False(array1.BitwiseEquals(array2));
            False(owner1.BitwiseEquals(owner2));
            True(owner1.BitwiseCompare(owner2) < 0);
            False(owner1.BitwiseEquals(owner2.Pointer));
            NotEqual(0, array1.BitwiseCompare(array2));
        }

        [Fact]
        public static void ResizeTest()
        {
            using var owner = UnmanagedMemoryPool<long>.Allocate(5);
            Span<long> array = owner.Span;
            array[0] = 10;
            array[1] = 20;
            array[2] = 30;
            array[3] = 40;
            array[4] = 50;
            Equal(50, array[4]);
            owner.Reallocate(2);
            array = owner.Span;
            Equal(2, array.Length);
            Equal(10, array[0]);
            Equal(20, array[1]);
        }

        [Fact]
        public static void SliceTest()
        {
            using var owner = UnmanagedMemoryPool<long>.Allocate(5);
            Span<long> span = owner.Span;
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

        [Fact]
        public static void Allocation()
        {
            using var manager = UnmanagedMemoryPool<long>.Allocate(2);
            Equal(2, manager.Length);

            Equal(sizeof(long) * 2, manager.Size);
            Equal(0, manager.Span[0]);
            Equal(0, manager.Span[1]);
            manager.Pointer[0] = 10L;
            manager.Pointer[1] = 20L;
            Equal(10L, manager.Span[0]);
            Equal(20L, manager.Span[1]);
            Equal(10L, manager.Memory.Span[0]);
            Equal(20L, manager.Memory.Span[1]);
        }

        [Fact]
        public static void Pooling()
        {
            using var pool = new UnmanagedMemoryPool<long>(10, trackAllocations: true);
            using var manager = pool.Rent(2);
            Equal(2, manager.Memory.Length);

            Equal(0, manager.Memory.Span[0]);
            Equal(0, manager.Memory.Span[1]);
            manager.Memory.Span[0] = 10L;
            manager.Memory.Span[1] = 20L;
            Equal(10L, manager.Memory.Span[0]);
            Equal(20L, manager.Memory.Span[1]);
        }

        [Fact]
        public static void EnumeratorTest()
        {
            using var owner = UnmanagedMemoryPool<int>.Allocate(3);
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

        [Fact]
        public static unsafe void ZeroMem()
        {
            using var memory = UnmanagedMemoryPool<byte>.Allocate(3, false);
            memory.Span[0] = 10;
            memory.Span[1] = 20;
            memory.Span[2] = 30;
            memory.Clear();
            foreach (ref var b in memory.Bytes)
                Equal(0, b);
            Equal(0, memory.Span[0]);
            Equal(0, memory.Span[1]);
            Equal(0, memory.Span[2]);
        }

        [Fact]
        public static async Task StreamInteropAsync()
        {
            using var memory = UnmanagedMemoryPool<ushort>.Allocate(3);
            using var ms = new MemoryStream();
            new ushort[] { 1, 2, 3 }.AsSpan().CopyTo(memory.Span);
            await memory.WriteToAsync(ms);
            Equal(6L, ms.Length);
            True(ms.TryGetBuffer(out var buffer));
            buffer.Array.ForEach((ref byte value, nint _) =>
            {
                if (value == 1)
                    value = 20;
            });
            ms.Position = 0;
            Equal(6, await memory.ReadFromAsync(ms));
            Equal(20, memory.Span[0]);
        }

        [Fact]
        public static void StreamInterop()
        {
            using var memory = UnmanagedMemoryPool<ushort>.Allocate(3);
            using var ms = new MemoryStream();
            new ushort[] { 1, 2, 3 }.AsSpan().CopyTo(memory.Span);
            memory.WriteTo(ms);
            Equal(6L, ms.Length);
            True(ms.TryGetBuffer(out var buffer));
            buffer.Array.ForEach((ref byte value, nint _) =>
            {
                if (value == 1)
                    value = 20;
            });
            ms.Position = 0;
            Equal(6, memory.ReadFrom(ms));
            Equal(20, memory.Span[0]);
        }

        [Fact]
        public static unsafe void ToStreamConversion()
        {
            using var memory = UnmanagedMemoryPool<byte>.Allocate(3, false);
            new byte[] { 10, 20, 30 }.AsSpan().CopyTo(memory.Bytes);
            using var stream = memory.AsStream();
            var bytes = new byte[3];
            Equal(3, stream.Read(bytes, 0, 3));
            Equal(10, bytes[0]);
            Equal(20, bytes[1]);
            Equal(30, bytes[2]);
        }

        [Fact]
        public static void CopyMemory()
        {
            using var memory1 = UnmanagedMemoryPool<int>.Allocate(3);
            memory1.Span[0] = 10;
            using var memory2 = memory1.Clone() as IUnmanagedMemoryOwner<int>;
            Equal(10, memory2.Span[0]);
            memory2.Span[0] = int.MaxValue;
            Equal(int.MaxValue, memory2.Span[0]);
            Equal(10, memory1.Span[0]);
        }

        [Fact]
        public static unsafe void Pinning()
        {
            using var memory = UnmanagedMemoryPool<int>.Allocate(3) as MemoryManager<int>;
            NotNull(memory);
            memory.GetSpan()[0] = 10;
            memory.GetSpan()[1] = 20;
            memory.GetSpan()[2] = 30;
            var handle = memory.Pin();
            Equal(10, *(int*)handle.Pointer);
            memory.Unpin();
            handle.Dispose();
            handle = memory.Pin(1);
            Equal(20, *(int*)handle.Pointer);
            memory.Unpin();
            handle.Dispose();
            handle = memory.Pin(2);
            Equal(30, *(int*)handle.Pointer);
            memory.Unpin();
            handle.Dispose();
        }
    }
}

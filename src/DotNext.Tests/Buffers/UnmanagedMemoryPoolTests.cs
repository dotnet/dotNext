using System;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Buffers
{
    [ExcludeFromCodeCoverage]
    public sealed class UnmanagedMemoryPoolTests : Assert
    {
        [Fact]
        public static void Allocation()
        {
            using (var manager = UnmanagedMemoryPool<long>.Allocate(2))
            {
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
        }

        [Fact]
        public static void Pooling()
        {
            using (var pool = new UnmanagedMemoryPool<long>(10, trackAllocations: true))
            using (var manager = pool.Rent(2))
            {
                Equal(2, manager.Memory.Length);

                Equal(0, manager.Memory.Span[0]);
                Equal(0, manager.Memory.Span[1]);
                manager.Memory.Span[0] = 10L;
                manager.Memory.Span[1] = 20L;
                Equal(10L, manager.Memory.Span[0]);
                Equal(20L, manager.Memory.Span[1]);
            }
        }

        [Fact]
        public static void EnumeratorTest()
        {
            using (var owner = UnmanagedMemoryPool<int>.Allocate(3))
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
        public static unsafe void ZeroMem()
        {
            using (var memory = UnmanagedMemoryPool<byte>.Allocate(3, false))
            {
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
        }

        [Fact]
        public static unsafe void ToStreamConversion()
        {
            using (var memory = UnmanagedMemoryPool<byte>.Allocate(3, false))
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
    }
}

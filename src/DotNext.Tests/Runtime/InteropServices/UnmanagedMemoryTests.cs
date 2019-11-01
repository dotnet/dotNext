using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Runtime.InteropServices
{
    [ExcludeFromCodeCoverage]
    public sealed class UnmanagedMemoryTests : Assert
    {
        private struct Data
        {
            internal int Field1, Field2;
        }

        [Fact]
        public static void BoxUnboxTest()
        {
            using (var owner = UnmanagedMemory<Data>.Box(new Data { Field1 = 10, Field2 = 20 }))
            {
                Data d = owner.Pointer.Value;
                Equal(10, d.Field1);
                Equal(20, d.Field2);
            }
        }

        [Fact]
        public static void UntypedMemoryTest()
        {
            using (var memory = new UnmanagedMemory(10))
            {
                Span<byte> bytes = memory.Bytes;
                Equal(10, memory.Size);
                Equal(10, bytes.Length);
                Equal(0, bytes[0]);
                Equal(0, bytes[9]);
                bytes[0] = 10;
                bytes[1] = 20;
                Equal(10, bytes[0]);
                Equal(20, bytes[1]);
                memory.Reallocate(12);
                bytes = memory.Bytes;
                Equal(12, memory.Size);
                Equal(10, bytes[0]);
                Equal(20, bytes[1]);
                var array = new byte[2];
                memory.WriteTo(array);
                Equal(10, array[0]);
                Equal(20, array[1]);
                array[0] = 30;
                array[1] = 40;
                memory.ReadFrom(array);
                Equal(30, bytes[0]);
                Equal(40, bytes[1]);
                bytes = memory;
                Equal(30, bytes[0]);
                Equal(40, bytes[1]);
                Pointer<byte> ptr = memory;
                Equal(30, ptr[0]);
                Equal(40, ptr[1]);
            }
        }

        [Fact]
        public static async Task StreamInteropAsync()
        {
            using (var memory = new UnmanagedMemory(3, false))
            using (var ms = new MemoryStream())
            {
                memory.Bytes[0] = 1;
                memory.Bytes[1] = 2;
                memory.Bytes[2] = 3;
                await memory.WriteToAsync(ms);
                Equal(3L, ms.Length);
                True(ms.TryGetBuffer(out var buffer));
                buffer.Array.ForEach((ref byte value, long index) =>
                {
                    if (value == 1)
                        value = 20;
                });
                ms.Position = 0;
                Equal(3, await memory.ReadFromAsync(ms));
                Equal(20, memory.Bytes[0]);
            }
        }

        [Fact]
        public static void StreamInterop()
        {
            using (var memory = new UnmanagedMemory(3, false))
            using (var ms = new MemoryStream())
            {
                memory.Bytes[0] = 1;
                memory.Bytes[1] = 2;
                memory.Bytes[2] = 3;
                memory.WriteTo(ms);
                Equal(3L, ms.Length);
                True(ms.TryGetBuffer(out var buffer));
                buffer.Array.ForEach((ref byte value, long index) =>
                {
                    if (value == 1)
                        value = 20;
                });
                ms.Position = 0;
                Equal(3, memory.ReadFrom(ms));
                Equal(20, memory.Bytes[0]);
            }
        }

        [Fact]
        public static unsafe void ToStreamConversion()
        {
            using (var memory = new UnmanagedMemory(3, false))
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
            using (var memory1 = new UnmanagedMemory(3))
            {
                memory1.Bytes[0] = 10;
                using (var memory2 = memory1.Copy())
                {
                    Equal(10, memory2.Bytes[0]);
                    memory2.Bytes[0] = byte.MaxValue;
                    Equal(byte.MaxValue, memory2.Bytes[0]);
                    Equal(10, memory1.Bytes[0]);
                }
            }
        }
    }
}

using System;
using Xunit;

namespace DotNext.Runtime.InteropServices
{
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
            }
        }
    }
}

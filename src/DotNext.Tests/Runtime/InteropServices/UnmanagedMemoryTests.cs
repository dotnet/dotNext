using System;
using Xunit;

namespace DotNext.Runtime.InteropServices
{
    public sealed class UnmanagedMemoryTests: Assert
    {
        private struct Data
        {
            internal int Field1, Field2;
        }

        [Fact]
        public void BoxUnboxTest()
        {
            using(var value = new UnmanagedMemory<Data>(new Data { Field1 = 10, Field2 = 20 }))
            {
                Data d = value;
                Equal(10, d.Field1);
                Equal(20, d.Field2);
                value.Dispose();
            }
        }

        [Fact]
        public void UntypedMemoryTest()
        {
            var memory = new UnmanagedMemory(10);
            try
            {
                Equal(10, memory.Size);
                Equal(0, memory[0]);
                Equal(0, memory[9]);
                memory[0] = 10;
                memory[1] = 20;
                Equal(10, memory[0]);
                Equal(20, memory[1]);
                memory.Size = 12;
                Equal(12, memory.Size);
                Equal(10, memory[0]);
                Equal(20, memory[1]);
            }
            finally
            {
                memory.Dispose();
            }
        }
    }
}

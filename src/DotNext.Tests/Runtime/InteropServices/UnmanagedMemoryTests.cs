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
    }
}

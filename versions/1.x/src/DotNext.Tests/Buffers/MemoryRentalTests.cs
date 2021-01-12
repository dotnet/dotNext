using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Buffers
{
    [ExcludeFromCodeCoverage]
    public sealed class MemoryRentalTests : Assert
    {
        [Fact]
        public static unsafe void StackAllocationTest()
        {
            MemoryRental<int> vector = stackalloc int[4];
            Equal(4, vector.Length);
            Equal(4, vector.Span.Length);
            vector[0] = 10;
            vector[1] = 20;
            vector[2] = 30;
            vector[3] = 40;
            Equal(10, vector.Span[0]);
            Equal(20, vector.Span[1]);
            Equal(30, vector.Span[2]);
            Equal(40, vector.Span[3]);
            vector.Dispose();
        }

        [Fact]
        public static void HeapAllocationTest()
        {
            var vector = new MemoryRental<int>(4);
            Equal(4, vector.Length);
            Equal(4, vector.Span.Length);
            vector[0] = 10;
            vector[1] = 20;
            vector[2] = 30;
            vector[3] = 40;
            Equal(10, vector.Span[0]);
            Equal(20, vector.Span[1]);
            Equal(30, vector.Span[2]);
            Equal(40, vector.Span[3]);
            vector.Dispose();
        }
    }
}

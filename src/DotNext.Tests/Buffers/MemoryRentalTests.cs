using System;
using System.Diagnostics.CodeAnalysis;
using static System.Runtime.CompilerServices.Unsafe;
using Xunit;

namespace DotNext.Buffers
{
    [ExcludeFromCodeCoverage]
    public sealed class MemoryRentalTests : Assert
    {
        [Fact]
        public static unsafe void StackAllocationTest()
        {
            using MemoryRental<int> vector = stackalloc int[4];
            False(vector.IsEmpty);
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
        }

        [Fact]
        public static void HeapAllocationTest()
        {
            using var vector = new MemoryRental<int>(4);
            False(vector.IsEmpty);
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
        }

        [Fact]
        public static unsafe void WrapArray()
        {
            int[] array = { 10, 20 };
            using var rental = new MemoryRental<int>(array);
            False(rental.IsEmpty);
            fixed (int* ptr = rental)
            {
                Equal(10, *ptr);
            }
            True(AreSame(ref rental[0], ref array[0]));
        }

        [Fact]
        public static void Default()
        {
            var rental = new MemoryRental<int>(Array.Empty<int>());
            True(rental.IsEmpty);
            Equal(0, rental.Length);
            True(rental.Span.IsEmpty);
            rental.Dispose();

            rental = default;
            True(rental.IsEmpty);
            Equal(0, rental.Length);
            True(rental.Span.IsEmpty);
            rental.Dispose();
        }
    }
}

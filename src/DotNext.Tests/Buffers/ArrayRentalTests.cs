using System;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Buffers
{
    [ExcludeFromCodeCoverage]
    public sealed class ArrayRentalTests : Assert
    {
        [Fact]
        public static void RentFromPool()
        {
            using var rental = new ArrayRental<char>(4);
            False(rental.IsEmpty);
            Equal(4, rental.Length);
            Equal(4, rental.Memory.Length);
            Equal(4, rental.Span.Length);
            Equal(4, rental.Segment.Count);
            rental[0] = 'a';
            rental[1] = 'b';
            rental[2] = 'c';
            rental[3] = 'd';
            True(new Span<char>(new[] { 'a', 'b', 'c', 'd' }).SequenceEqual(rental.Span));
        }

        [Fact]
        public static unsafe void WrapArray()
        {
            int[] array = { 10, 20 };
            using var rental = new ArrayRental<int>(array);
            False(rental.IsEmpty);
            fixed (int* ptr = rental)
            {
                Equal(10, *ptr);
            }
            Same(array, rental.Segment.Array);
        }

        [Fact]
        public static void Default()
        {
            var rental = new ArrayRental<int>(Array.Empty<int>());
            True(rental.IsEmpty);
            Equal(0, rental.Length);
            True(rental.Span.IsEmpty);
            True(rental.Memory.IsEmpty);
            True(rental.Segment.Count == 0);
            Empty(rental.Segment);
            rental.Dispose();

            rental = default;
            True(rental.IsEmpty);
            Equal(0, rental.Length);
            True(rental.Span.IsEmpty);
            True(rental.Memory.IsEmpty);
            True(rental.Segment.Count == 0);
            rental.Dispose();
            Empty(rental.Segment);
        }
    }
}

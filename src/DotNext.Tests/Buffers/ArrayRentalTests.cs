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
            using (var rental = new ArrayRental<char>(4))
            {
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
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    public sealed class SpanTests : Assert
    {
        [Fact]
        public static void BitwiseEquality()
        {
            Span<Guid> array1 = new Guid[] { Guid.Empty, Guid.NewGuid(), Guid.NewGuid() };
            Span<Guid> array2 = new Guid[] { Guid.Empty, array1[1], array1[2] };
            True(array1.SequenceEqual(array2));
            True(array1.BitwiseEquals(array2));
            array2[1] = Guid.Empty;
            False(array1.SequenceEqual(array2));
            False(array1.BitwiseEquals(array2));
        }

        [Fact]
        public static void BitwiseComparison()
        {
            Span<Guid> array1 = new Guid[] { Guid.Empty, Guid.NewGuid(), Guid.NewGuid() };
            Span<Guid> array2 = new Guid[] { Guid.Empty, array1[1], array1[2] };
            Equal(0, array1.BitwiseCompare(array2));
            array2[1] = Guid.Empty;
            True(array1.BitwiseCompare(array2) > 0);
        }

        [Fact]
        public static void Sorting()
        {
            Span<ulong> span = new ulong[] { 3, 2, 6, 4 };
            span.Sort();
            Equal(2UL, span[0]);
            Equal(3UL, span[1]);
            Equal(4UL, span[2]);
            Equal(6UL, span[3]);
        }

        [Fact]
        public static void IndexOf()
        {
            ReadOnlySpan<ulong> span = new ulong[] { 3, 2, 6, 4 };
            Equal(1, span.IndexOf(2UL, 0, EqualityComparer<ulong>.Default.Equals));
            Equal(3, span.IndexOf(4UL, 0, EqualityComparer<ulong>.Default.Equals));
            Equal(3UL, span[0]);
            Equal(2UL, span[1]);
        }

        [Fact]
        public static void ForEach()
        {
            Span<long> span = new long[] { 3, 2, 6, 4 };
            span.ForEach((ref long value, int index) => value += index);
            Equal(3, span[0]);
            Equal(3, span[1]);
            Equal(8, span[2]);
            Equal(7, span[3]);
        }
    }
}
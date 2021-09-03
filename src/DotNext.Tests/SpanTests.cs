using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext
{
    using Buffers;

    [ExcludeFromCodeCoverage]
    public sealed class SpanTests : Test
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
        public static unsafe void SortingUsingPointer()
        {
            Span<ulong> span = new ulong[] { 3, 2, 6, 4 };
            span.Sort(&Sort);
            Equal(6UL, span[0]);
            Equal(4UL, span[1]);
            Equal(3UL, span[2]);
            Equal(2UL, span[3]);

            static int Sort(ulong x, ulong y) => (int)(y - x);
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

        [Fact]
        public static void TrimByLength1()
        {
            Span<int> span = new int[] { 1, 2, 3 };
            span = span.TrimLength(4);
            Equal(3, span.Length);
            span = span.TrimLength(2);
            Equal(2, span.Length);
            Equal(1, span[0]);
            Equal(2, span[1]);
        }

        [Fact]
        public static void TrimByLength2()
        {
            ReadOnlySpan<int> span = new int[] { 1, 2, 3 };
            span = span.TrimLength(4);
            Equal(3, span.Length);
            span = span.TrimLength(2);
            Equal(2, span.Length);
            Equal(1, span[0]);
            Equal(2, span[1]);
        }

        private static string ToHexSlow(byte[] data, bool lowercased)
            => string.Join(string.Empty, Array.ConvertAll(data, i => i.ToString(lowercased ? "x2" : "X2", null)));

        [Theory]
        [InlineData(0, true)]
        [InlineData(128, true)]
        [InlineData(2048, true)]
        [InlineData(0, false)]
        [InlineData(128, false)]
        [InlineData(2048, false)]
        public static void ToHexConversion(int arraySize, bool lowercased)
        {
            var data = RandomBytes(arraySize);
            Equal(ToHexSlow(data, lowercased), new ReadOnlySpan<byte>(data).ToHex(lowercased));
        }

        [Fact]
        public static void ToHexConversionVarLength()
        {
            ReadOnlySpan<byte> data = new byte[] { 1, 2 };
            char[] encoded = new char[1];
            Equal(0, data.ToHex(encoded));
            encoded = new char[2];
            Equal(2, data.ToHex(encoded));
            Equal('0', encoded[0]);
            Equal('1', encoded[1]);
        }

        [Fact]
        public static void FromHexConversionVarLength()
        {
            ReadOnlySpan<char> data = new char[] { 'F', 'F', 'A' };
            var decoded = new byte[1];
            Equal(1, data.FromHex(decoded));
            Equal(byte.MaxValue, decoded[0]);
            data = "ABBA".AsSpan();
            decoded = new byte[2];
            Equal(2, data.FromHex(decoded));
            Equal(0xAB, decoded[0]);
            Equal(0xBA, decoded[1]);
            data = "abba".AsSpan();
            Equal(2, data.FromHex(decoded));
            Equal(0xAB, decoded[0]);
            Equal(0xBA, decoded[1]);
            data = default;
            Equal(0, data.FromHex(decoded));
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(128, true)]
        [InlineData(2048, true)]
        [InlineData(0, false)]
        [InlineData(128, false)]
        [InlineData(2048, false)]
        public static void FromHexConversion(int arraySize, bool lowercased)
        {
            var data = RandomBytes(arraySize);
            ReadOnlySpan<char> hex = ToHexSlow(data, lowercased);
            Equal(data, hex.FromHex());
        }

        public static IEnumerable<object[]> TestAllocators()
        {
            yield return new object[] { null };
            yield return new object[] { MemoryAllocator.CreateArrayAllocator<char>() };
            yield return new object[] { ArrayPool<char>.Shared.ToAllocator() };
        }

        [Theory]
        [MemberData(nameof(TestAllocators))]
        public static void Concatenation(MemoryAllocator<char> allocator)
        {
            MemoryOwner<char> owner = string.Empty.AsSpan().Concat(string.Empty, allocator);
            True(owner.IsEmpty);
            True(owner.Memory.IsEmpty);
            owner.Dispose();

            owner = "Hello, ".AsSpan().Concat("world!", allocator);
            False(owner.IsEmpty);
            False(owner.Memory.IsEmpty);
            Equal("Hello, world!", new string(owner.Memory.Span));
            owner.Dispose();

            owner = "Hello, ".AsSpan().Concat("world", "!", allocator);
            False(owner.IsEmpty);
            False(owner.Memory.IsEmpty);
            Equal("Hello, world!", new string(owner.Memory.Span));
            owner.Dispose();
        }

        [Theory]
        [InlineData(new int[] { 10, 20, 30 }, new int[] { 0, 0, 0 })]
        [InlineData(new int[] { 10, 20, 30 }, new int[] { 0, 0 })]
        [InlineData(new int[] { 10, 20, 30 }, new int[] { 0, 0, 0, 0 })]
        public static void Copy(int[] source, int[] destination)
        {
            ReadOnlySpan<int> src = source;
            Span<int> dst = destination;
            src.CopyTo(dst, out var writtenCount);
            Equal(Math.Min(src.Length, dst.Length), writtenCount);

            for (var i = 0; i < writtenCount; i++)
                Equal(src[i], dst[i]);
        }

        [Fact]
        public static void BufferizeSpan()
        {
            var owner = ReadOnlySpan<byte>.Empty.Copy();
            True(owner.IsEmpty);

            owner = new ReadOnlySpan<byte>(new byte[] { 10, 20, 30 }).Copy();
            Equal(3, owner.Length);
            Equal(10, owner[0]);
            Equal(20, owner[1]);
            Equal(30, owner[2]);
            owner.Dispose();
        }

        [Fact]
        public static void Tuple0ToSpan()
        {
            var tuple = new ValueTuple();
            True(tuple.AsSpan<int>().IsEmpty);
        }

        [Fact]
        public static void Tuple0ToReadOnlySpan()
        {
            var tuple = new ValueTuple();
            True(tuple.AsReadOnlySpan<int>().IsEmpty);
        }

        [Fact]
        public static void Tuple1ToSpan()
        {
            var tuple = new ValueTuple<int>(42);
            var span = tuple.AsSpan();
            False(span.IsEmpty);
            Equal(1, span.Length);
            Equal(42, span[0]);

            span[0] = 52;
            Equal(52, tuple.Item1);
        }

        [Fact]
        public static void Tuple1ToReadOnlySpan()
        {
            var span = new ValueTuple<int>(42).AsReadOnlySpan();
            False(span.IsEmpty);
            Equal(1, span.Length);
            Equal(42, span[0]);
        }

        [Fact]
        public static void Tuple2ToSpan()
        {
            var tuple = (42, 43);
            var span = tuple.AsSpan();
            False(span.IsEmpty);
            Equal(2, span.Length);
            Equal(42, span[0]);
            Equal(43, span[1]);

            span[0] = 52;
            span[1] = 53;
            Equal(52, tuple.Item1);
            Equal(53, tuple.Item2);
        }

        [Fact]
        public static void Tuple2ToReadOnlySpan()
        {
            var span = (42, 43).AsReadOnlySpan();
            False(span.IsEmpty);
            Equal(2, span.Length);
            Equal(42, span[0]);
            Equal(43, span[1]);
        }

        [Fact]
        public static void Tuple3ToSpan()
        {
            var tuple = (42, 43, 44);
            var span = tuple.AsSpan();
            False(span.IsEmpty);
            Equal(3, span.Length);
            Equal(42, span[0]);
            Equal(43, span[1]);
            Equal(44, span[2]);

            span[0] = 52;
            span[1] = 53;
            span[2] = 54;
            Equal(52, tuple.Item1);
            Equal(53, tuple.Item2);
            Equal(54, tuple.Item3);
        }

        [Fact]
        public static void Tuple3ToReadOnlySpan()
        {
            var span = (42, 43, 44).AsReadOnlySpan();
            False(span.IsEmpty);
            Equal(3, span.Length);
            Equal(42, span[0]);
            Equal(43, span[1]);
            Equal(44, span[2]);
        }

        [Fact]
        public static void Tuple4ToSpan()
        {
            var tuple = (42, 43, 44, 45);
            var span = tuple.AsSpan();
            False(span.IsEmpty);
            Equal(4, span.Length);
            Equal(42, span[0]);
            Equal(43, span[1]);
            Equal(44, span[2]);
            Equal(45, span[3]);

            span[0] = 52;
            span[1] = 53;
            span[2] = 54;
            span[3] = 55;
            Equal(52, tuple.Item1);
            Equal(53, tuple.Item2);
            Equal(54, tuple.Item3);
            Equal(55, tuple.Item4);
        }

        [Fact]
        public static void Tuple4ToReadOnlySpan()
        {
            var span = (42, 43, 44, 45).AsReadOnlySpan();
            False(span.IsEmpty);
            Equal(4, span.Length);
            Equal(42, span[0]);
            Equal(43, span[1]);
            Equal(44, span[2]);
            Equal(45, span[3]);
        }

        [Fact]
        public static void Tuple5ToSpan()
        {
            var tuple = (42, 43, 44, 45, 46);
            var span = tuple.AsSpan();
            False(span.IsEmpty);
            Equal(5, span.Length);
            Equal(42, span[0]);
            Equal(43, span[1]);
            Equal(44, span[2]);
            Equal(45, span[3]);
            Equal(46, span[4]);

            span[0] = 52;
            span[1] = 53;
            span[2] = 54;
            span[3] = 55;
            span[4] = 56;
            Equal(52, tuple.Item1);
            Equal(53, tuple.Item2);
            Equal(54, tuple.Item3);
            Equal(55, tuple.Item4);
            Equal(56, tuple.Item5);
        }

        [Fact]
        public static void Tuple5ToReadOnlySpan()
        {
            var span = (42, 43, 44, 45, 46).AsReadOnlySpan();
            False(span.IsEmpty);
            Equal(5, span.Length);
            Equal(42, span[0]);
            Equal(43, span[1]);
            Equal(44, span[2]);
            Equal(45, span[3]);
            Equal(46, span[4]);
        }

        [Fact]
        public static void Tuple6ToSpan()
        {
            var tuple = (42, 43, 44, 45, 46, 47);
            var span = tuple.AsSpan();
            False(span.IsEmpty);
            Equal(6, span.Length);
            Equal(42, span[0]);
            Equal(43, span[1]);
            Equal(44, span[2]);
            Equal(45, span[3]);
            Equal(46, span[4]);
            Equal(47, span[5]);

            span[0] = 52;
            span[1] = 53;
            span[2] = 54;
            span[3] = 55;
            span[4] = 56;
            span[5] = 57;
            Equal(52, tuple.Item1);
            Equal(53, tuple.Item2);
            Equal(54, tuple.Item3);
            Equal(55, tuple.Item4);
            Equal(56, tuple.Item5);
            Equal(57, tuple.Item6);
        }

        [Fact]
        public static void Tuple6ToReadOnlySpan()
        {
            var span = (42, 43, 44, 45, 46, 47).AsReadOnlySpan();
            False(span.IsEmpty);
            Equal(6, span.Length);
            Equal(42, span[0]);
            Equal(43, span[1]);
            Equal(44, span[2]);
            Equal(45, span[3]);
            Equal(46, span[4]);
            Equal(47, span[5]);
        }

        [Fact]
        public static void Tuple7ToSpan()
        {
            var tuple = (42, 43, 44, 45, 46, 47, 48);
            var span = tuple.AsSpan();
            False(span.IsEmpty);
            Equal(7, span.Length);
            Equal(42, span[0]);
            Equal(43, span[1]);
            Equal(44, span[2]);
            Equal(45, span[3]);
            Equal(46, span[4]);
            Equal(47, span[5]);
            Equal(48, span[6]);

            span[0] = 52;
            span[1] = 53;
            span[2] = 54;
            span[3] = 55;
            span[4] = 56;
            span[5] = 57;
            span[6] = 58;
            Equal(52, tuple.Item1);
            Equal(53, tuple.Item2);
            Equal(54, tuple.Item3);
            Equal(55, tuple.Item4);
            Equal(56, tuple.Item5);
            Equal(57, tuple.Item6);
            Equal(58, tuple.Item7);
        }

        [Fact]
        public static void Tuple7ToReadOnlySpan()
        {
            var span = (42, 43, 44, 45, 46, 47, 48).AsReadOnlySpan();
            False(span.IsEmpty);
            Equal(7, span.Length);
            Equal(42, span[0]);
            Equal(43, span[1]);
            Equal(44, span[2]);
            Equal(45, span[3]);
            Equal(46, span[4]);
            Equal(47, span[5]);
            Equal(48, span[6]);
        }

        [Fact]
        public static unsafe void ForEachUsingPointer()
        {
            int[] array = { 1, 2, 3 };
            array.AsSpan().ForEach(&Exists, array);

            static void Exists(ref int item, int[] array) => Contains(item, array);
        }
    }
}
using System;
using System.Buffers;
using System.Collections.Generic;
using Xunit;

namespace DotNext
{
    using Buffers;

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
        public static void Sorting()
        {
            Span<ulong> span = new ulong[] { 3, 2, 6, 4 };
            span.Sort();
            Equal(2UL, span[0]);
            Equal(3UL, span[1]);
            Equal(4UL, span[2]);
            Equal(6UL, span[3]);

            span.Sort((x1, x2) => (int)(x2 - x1));
            Equal(6UL, span[0]);
            Equal(4UL, span[1]);
            Equal(3UL, span[2]);
            Equal(2UL, span[3]);
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
            var data = new byte[arraySize];
            var rnd = new Random();
            rnd.NextBytes(data);
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
            var data = new byte[arraySize];
            var rnd = new Random();
            rnd.NextBytes(data);
            ReadOnlySpan<char> hex = ToHexSlow(data, lowercased);
            Equal(data, hex.FromHex());
        }

        private struct TwoIDs
        {
            internal Guid First;
            internal Guid Second;
        }

        [Fact]
        public static void ReadValues()
        {
            var ids = new TwoIDs { First = Guid.NewGuid(), Second = Guid.NewGuid() };
            var span = Span.AsReadOnlyBytes(in ids);
            Equal(ids.First, Span.Read<Guid>(ref span));
            Equal(ids.Second, Span.Read<Guid>(ref span));
            True(span.IsEmpty);
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
    }
}
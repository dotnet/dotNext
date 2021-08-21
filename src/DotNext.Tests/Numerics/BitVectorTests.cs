using System;
using Xunit;

namespace DotNext.Numerics
{
    public sealed class BitVectorTests : Test
    {
        private static ReadOnlySpan<bool> CreateVector(int size, bool value)
        {
            var result = new bool[size];
            Array.Fill(result, value);
            return result;
        }

        [Fact]
        public static void BitsToByte()
        {
            Equal(0, BitVector.ToByte(ReadOnlySpan<bool>.Empty));
            Equal(3, BitVector.ToByte(stackalloc bool[] { true, true }));
            Equal(8, BitVector.ToByte(stackalloc bool[] { false, false, false, true }));
            Equal(byte.MaxValue, BitVector.ToByte(CreateVector(8, true)));
        }

        [Fact]
        public static void BitsToInt16()
        {
            Equal(0, BitVector.ToInt16(ReadOnlySpan<bool>.Empty));
            Equal(3, BitVector.ToInt16(stackalloc bool[] { true, true }));
            Equal(8, BitVector.ToInt16(stackalloc bool[] { false, false, false, true }));
            Equal(short.MaxValue, BitVector.ToInt16(CreateVector(15, true)));
            Equal(-1, BitVector.ToInt16(CreateVector(16, true)));
        }

        [Fact]
        public static void BitsToUInt16()
        {
            Equal(0, BitVector.ToUInt16(ReadOnlySpan<bool>.Empty));
            Equal(3, BitVector.ToUInt16(stackalloc bool[] { true, true }));
            Equal(8, BitVector.ToUInt16(stackalloc bool[] { false, false, false, true }));
            Equal(ushort.MaxValue, BitVector.ToUInt16(CreateVector(16, true)));
        }

        [Fact]
        public static void BitsToInt32()
        {
            Equal(0, BitVector.ToInt32(ReadOnlySpan<bool>.Empty));
            Equal(3, BitVector.ToInt32(stackalloc bool[] { true, true }));
            Equal(8, BitVector.ToInt32(stackalloc bool[] { false, false, false, true }));
            Equal(int.MaxValue, BitVector.ToInt32(CreateVector(31, true)));
            Equal(-1, BitVector.ToInt32(CreateVector(32, true)));
        }

        [Fact]
        public static void BitsToUInt32()
        {
            Equal(0U, BitVector.ToUInt32(ReadOnlySpan<bool>.Empty));
            Equal(3U, BitVector.ToUInt32(stackalloc bool[] { true, true }));
            Equal(8U, BitVector.ToUInt32(stackalloc bool[] { false, false, false, true }));
            Equal(uint.MaxValue, BitVector.ToUInt32(CreateVector(32, true)));
        }

        [Fact]
        public static void BitsToInt64()
        {
            Equal(0L, BitVector.ToInt64(ReadOnlySpan<bool>.Empty));
            Equal(3L, BitVector.ToInt64(stackalloc bool[] { true, true }));
            Equal(8L, BitVector.ToInt64(stackalloc bool[] { false, false, false, true }));
            Equal(long.MaxValue, BitVector.ToInt64(CreateVector(63, true)));
            Equal(-1L, BitVector.ToInt32(CreateVector(64, true)));
        }

        [Fact]
        public static void BitsToUInt64()
        {
            Equal(0UL, BitVector.ToUInt64(ReadOnlySpan<bool>.Empty));
            Equal(3UL, BitVector.ToUInt64(stackalloc bool[] { true, true }));
            Equal(8UL, BitVector.ToUInt64(stackalloc bool[] { false, false, false, true }));
            Equal(ulong.MaxValue, BitVector.ToUInt64(CreateVector(64, true)));
        }
    }
}
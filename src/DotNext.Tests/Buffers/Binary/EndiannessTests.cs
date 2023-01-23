using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DotNext.Buffers.Binary
{
    [ExcludeFromCodeCoverage]
    public sealed class SequenceReaderExtensionsTests : Test
    {
        [Theory]
        [InlineData(4)]
        [InlineData(10)]
        [InlineData(16 + 16 + 10)] // two 256 bit vectors, one 128 bit vector, and 2 elements
        public static void ReverseEndiannessInt16(int size)
        {
            var expected = new short[size];
            Random.Shared.NextBytes(MemoryMarshal.AsBytes<short>(expected));

            var actual = expected.ToArray();
            ReverseEndianessSlow(actual);
            Endianness.ReverseEndianness(actual);
            Equal(expected, actual);

            static void ReverseEndianessSlow(Span<short> values)
            {
                foreach (ref var item in values)
                    item = BinaryPrimitives.ReverseEndianness(item);
            }
        }

        [Theory]
        [InlineData(4)]
        [InlineData(10)]
        [InlineData(16 + 16 + 10)] // two 256 bit vectors, one 128 bit vector, and 2 elements
        public static void ReverseEndiannessUInt16(int size)
        {
            var expected = new ushort[size];
            Random.Shared.NextBytes(MemoryMarshal.AsBytes<ushort>(expected));

            var actual = expected.ToArray();
            ReverseEndianessSlow(actual);
            Endianness.ReverseEndianness(actual);
            Equal(expected, actual);

            static void ReverseEndianessSlow(Span<ushort> values)
            {
                foreach (ref var item in values)
                    item = BinaryPrimitives.ReverseEndianness(item);
            }
        }

        [Theory]
        [InlineData(2)]
        [InlineData(5)]
        [InlineData(8 + 8 + 6)] // two 256 bit vectors, one 128 bit vector, and 2 elements
        public static void ReverseEndiannessInt32(int size)
        {
            var expected = new int[size];
            Random.Shared.NextBytes(MemoryMarshal.AsBytes<int>(expected));

            var actual = expected.ToArray();
            ReverseEndianessSlow(actual);
            Endianness.ReverseEndianness(actual);
            Equal(expected, actual);

            static void ReverseEndianessSlow(Span<int> values)
            {
                foreach (ref var item in values)
                    item = BinaryPrimitives.ReverseEndianness(item);
            }
        }

        [Theory]
        [InlineData(2)]
        [InlineData(5)]
        [InlineData(8 + 8 + 6)] // two 256 bit vectors, one 128 bit vector, and 2 elements
        public static void ReverseEndiannessUInt32(int size)
        {
            var expected = new uint[size];
            Random.Shared.NextBytes(MemoryMarshal.AsBytes<uint>(expected));

            var actual = expected.ToArray();
            ReverseEndianessSlow(actual);
            Endianness.ReverseEndianness(actual);
            Equal(expected, actual);

            static void ReverseEndianessSlow(Span<uint> values)
            {
                foreach (ref var item in values)
                    item = BinaryPrimitives.ReverseEndianness(item);
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(4 + 4 + 3)]
        public static void ReverseEndiannessInt64(int size)
        {
            var expected = new long[size];
            Random.Shared.NextBytes(MemoryMarshal.AsBytes<long>(expected));

            var actual = expected.ToArray();
            ReverseEndianessSlow(actual);
            Endianness.ReverseEndianness(actual);
            Equal(expected, actual);

            static void ReverseEndianessSlow(Span<long> values)
            {
                foreach (ref var item in values)
                    item = BinaryPrimitives.ReverseEndianness(item);
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(4 + 4 + 3)]
        public static void ReverseEndiannessUInt64(int size)
        {
            var expected = new ulong[size];
            Random.Shared.NextBytes(MemoryMarshal.AsBytes<ulong>(expected));

            var actual = expected.ToArray();
            ReverseEndianessSlow(actual);
            Endianness.ReverseEndianness(actual);
            Equal(expected, actual);

            static void ReverseEndianessSlow(Span<ulong> values)
            {
                foreach (ref var item in values)
                    item = BinaryPrimitives.ReverseEndianness(item);
            }
        }
    }
}
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DotNext.Buffers
{
    [ExcludeFromCodeCoverage]
    public sealed class SequenceReaderExtensionsTests : Test
    {
        [Theory]
        [InlineData(4)]
        [InlineData(10)]
        [InlineData(16 + 16 + 10)] // two 256 bit vectors, one 128 bit vector, and 2 elements
        public static void ReadInt16VectorFromSequence(int size)
        {
            var expected = new short[size];
            Random.Shared.NextBytes(MemoryMarshal.AsBytes<short>(expected));

            var actual = expected.ToArray();
            ReverseEndianessSlow(actual);
            var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(MemoryMarshal.AsBytes<short>(actual).ToArray()));
            True(BitConverter.IsLittleEndian ? reader.TryReadBigEndian(actual) : reader.TryReadLittleEndian(actual));
            Equal(expected, actual);
        }

        [Theory]
        [InlineData(2)]
        [InlineData(5)]
        [InlineData(8 + 8 + 6)] // two 256 bit vectors, one 128 bit vector, and 2 elements
        public static void ReadInt32VectorFromSequence(int size)
        {
            var expected = new int[size];
            Random.Shared.NextBytes(MemoryMarshal.AsBytes<int>(expected));

            var actual = expected.ToArray();
            ReverseEndianessSlow(actual);
            var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(MemoryMarshal.AsBytes<int>(actual).ToArray()));
            True(BitConverter.IsLittleEndian ? reader.TryReadBigEndian(actual) : reader.TryReadLittleEndian(actual));
            Equal(expected, actual);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(4 + 4 + 3)]
        public static void ReadInt64VectorFromSequence(int size)
        {
            var expected = new long[size];
            Random.Shared.NextBytes(MemoryMarshal.AsBytes<long>(expected));

            var actual = expected.ToArray();
            ReverseEndianessSlow(actual);
            var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(MemoryMarshal.AsBytes<long>(actual).ToArray()));
            True(BitConverter.IsLittleEndian ? reader.TryReadBigEndian(actual) : reader.TryReadLittleEndian(actual));
            Equal(expected, actual);
        }

        [Theory]
        [InlineData(4)]
        [InlineData(10)]
        [InlineData(16 + 16 + 10)]
        public static void ReverseEndianessInt16(int size)
        {
            var expected = new short[size];
            Random.Shared.NextBytes(MemoryMarshal.AsBytes<short>(expected));

            var actual = expected.ToArray();
            actual.AsSpan().ReverseEndianess();
            ReverseEndianessSlow(expected);

            Equal(expected, actual);
        }

        [Theory]
        [InlineData(2)]
        [InlineData(5)]
        [InlineData(8 + 8 + 6)]
        public static void ReverseEndianessInt32(int size)
        {
            var expected = new int[size];
            Random.Shared.NextBytes(MemoryMarshal.AsBytes<int>(expected));

            var actual = expected.ToArray();
            actual.AsSpan().ReverseEndianess();
            ReverseEndianessSlow(expected);

            Equal(expected, actual);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(4 + 4 + 3)]
        public static void ReverseEndianessInt64(int size)
        {
            var expected = new long[size];
            Random.Shared.NextBytes(MemoryMarshal.AsBytes<long>(expected));

            var actual = expected.ToArray();
            actual.AsSpan().ReverseEndianess();
            ReverseEndianessSlow(expected);

            Equal(expected, actual);
        }

        private static void ReverseEndianessSlow(Span<short> values)
        {
            foreach (ref var item in values)
                item = BinaryPrimitives.ReverseEndianness(item);
        }

        private static void ReverseEndianessSlow(Span<int> values)
        {
            foreach (ref var item in values)
                item = BinaryPrimitives.ReverseEndianness(item);
        }

        private static void ReverseEndianessSlow(Span<long> values)
        {
            foreach (ref var item in values)
                item = BinaryPrimitives.ReverseEndianness(item);
        }
    }
}
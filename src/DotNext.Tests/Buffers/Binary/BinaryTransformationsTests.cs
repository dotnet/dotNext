using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DotNext.Buffers.Binary
{
    [ExcludeFromCodeCoverage]
    public sealed class BinaryTransformationsTests : Test
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
            BinaryTransformations.ReverseEndianness(actual);
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
            BinaryTransformations.ReverseEndianness(actual);
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
            BinaryTransformations.ReverseEndianness(actual);
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
            BinaryTransformations.ReverseEndianness(actual);
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
            BinaryTransformations.ReverseEndianness(actual);
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
            BinaryTransformations.ReverseEndianness(actual);
            Equal(expected, actual);

            static void ReverseEndianessSlow(Span<ulong> values)
            {
                foreach (ref var item in values)
                    item = BinaryPrimitives.ReverseEndianness(item);
            }
        }

        [Theory]
        [InlineData(32 + 16 + 3)]
        [InlineData(32 + 3)]
        [InlineData(3)]
        public static void BitwiseAnd(int size)
        {
            var x = new byte[size];
            Random.Shared.NextBytes(x);

            var y = new byte[size];
            Random.Shared.NextBytes(y);

            var expected = BitwiseAndSlow(x, y);
            BinaryTransformations.BitwiseAnd<byte>(x, y);
            Equal(expected, y);

            static byte[] BitwiseAndSlow(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y)
            {
                Equal(x.Length, y.Length);
                var result = new byte[x.Length];

                for (var i = 0; i < x.Length; i++)
                    result[i] = (byte)(x[i] & y[i]);

                return result;
            }
        }

        [Theory]
        [InlineData(32 + 16 + 3)]
        [InlineData(32 + 3)]
        [InlineData(3)]
        public static void BitwiseAndNot(int size)
        {
            var x = new byte[size];
            Random.Shared.NextBytes(x);

            var y = new byte[size];
            Random.Shared.NextBytes(y);

            var expected = BitwiseAndNotSlow(x, y);
            BinaryTransformations.AndNot<byte>(x, y);
            Equal(expected, y);

            static byte[] BitwiseAndNotSlow(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y)
            {
                Equal(x.Length, y.Length);
                var result = new byte[x.Length];

                for (var i = 0; i < x.Length; i++)
                    result[i] = (byte)(x[i] & ~y[i]);

                return result;
            }
        }

        [Theory]
        [InlineData(32 + 16 + 3)]
        [InlineData(32 + 3)]
        [InlineData(3)]
        public static void BitwiseOr(int size)
        {
            var x = new byte[size];
            Random.Shared.NextBytes(x);

            var y = new byte[size];
            Random.Shared.NextBytes(y);

            var expected = BitwiseOrSlow(x, y);
            BinaryTransformations.BitwiseOr<byte>(x, y);
            Equal(expected, y);

            static byte[] BitwiseOrSlow(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y)
            {
                Equal(x.Length, y.Length);
                var result = new byte[x.Length];

                for (var i = 0; i < x.Length; i++)
                    result[i] = (byte)(x[i] | y[i]);

                return result;
            }
        }

        [Theory]
        [InlineData(32 + 16 + 3)]
        [InlineData(32 + 3)]
        [InlineData(3)]
        public static void BitwiseXor(int size)
        {
            var x = new byte[size];
            Random.Shared.NextBytes(x);

            var y = new byte[size];
            Random.Shared.NextBytes(y);

            var expected = BitwiseXorSlow(x, y);
            BinaryTransformations.BitwiseXor<byte>(x, y);
            Equal(expected, y);

            static byte[] BitwiseXorSlow(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y)
            {
                Equal(x.Length, y.Length);
                var result = new byte[x.Length];

                for (var i = 0; i < x.Length; i++)
                    result[i] = (byte)(x[i] ^ y[i]);

                return result;
            }
        }

        [Theory]
        [InlineData(32 + 16 + 3)]
        [InlineData(32 + 3)]
        [InlineData(3)]
        public static void OnesComplement(int size)
        {
            var x = new byte[size];
            Random.Shared.NextBytes(x);

            var expected = OnesComplementSlow(x);
            BinaryTransformations.OnesComplement<byte>(x);
            Equal(expected, x);

            static byte[] OnesComplementSlow(ReadOnlySpan<byte> x)
            {
                var result = new byte[x.Length];

                for (var i = 0; i < x.Length; i++)
                    result[i] = (byte)(~x[i]);

                return result;
            }
        }
    }
}
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace DotNext.Buffers.Binary;

public sealed class BinaryTransformationsTests : Test
{
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
        var x = new uint[size];
        Random.Shared.NextBytes(MemoryMarshal.AsBytes<uint>(x));

        var y = new uint[size];
        Random.Shared.NextBytes(MemoryMarshal.AsBytes<uint>(y));

        var expected = BitwiseXorSlow(x, y);
        BinaryTransformations.BitwiseXor<uint>(x, y);
        Equal(expected, y);

        static uint[] BitwiseXorSlow(ReadOnlySpan<uint> x, ReadOnlySpan<uint> y)
        {
            Equal(x.Length, y.Length);
            var result = new uint[x.Length];

            for (var i = 0; i < x.Length; i++)
                result[i] = x[i] ^ y[i];

            return result;
        }
    }

    [Theory]
    [InlineData(32 + 16 + 3)]
    [InlineData(32 + 3)]
    [InlineData(3)]
    public static void OnesComplement(int size)
    {
        var x = new uint[size];
        Random.Shared.NextBytes(MemoryMarshal.AsBytes<uint>(x));

        var expected = OnesComplementSlow(x);
        BinaryTransformations.OnesComplement<uint>(x);
        Equal(expected, x);

        static uint[] OnesComplementSlow(ReadOnlySpan<uint> x)
        {
            var result = new uint[x.Length];

            for (var i = 0; i < x.Length; i++)
                result[i] = ~x[i];

            return result;
        }
    }
}
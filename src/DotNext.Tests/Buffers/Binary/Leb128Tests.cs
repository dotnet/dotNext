using System.Numerics;

namespace DotNext.Buffers.Binary;

public sealed class Leb128Tests : Test
{
    private static void EncodeDecode<T>(ReadOnlySpan<T> values)
        where T : struct, IBinaryInteger<T>
    {
        Span<byte> buffer = stackalloc byte[Leb128<T>.MaxSizeInBytes];

        foreach (var expected in values)
        {
            True(Leb128<T>.TryGetBytes(expected, buffer, out var bytesWritten));
            True(Leb128<T>.TryParse(buffer, out var actual, out var bytesConsumed));
            Equal(bytesWritten, bytesConsumed);
            Equal(expected, actual);
        }
    }

    [Fact]
    public static void EncodeDecodeInt32() => EncodeDecode([0, int.MaxValue, int.MinValue, 0x80, -1]);
    
    [Fact]
    public static void EncodeDecodeInt64() => EncodeDecode([0L, long.MaxValue, long.MinValue, 0x80L, -1L]);

    [Fact]
    public static void EncodeDecodeInt128() => EncodeDecode([0, Int128.MaxValue, Int128.MinValue, 0x80, Int128.NegativeOne]);

    [Fact]
    public static void EncodeDecodeUInt32() => EncodeDecode([uint.MinValue, uint.MaxValue, 0x80U]);
}
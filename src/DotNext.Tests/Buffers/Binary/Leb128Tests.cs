using System.Numerics;

namespace DotNext.Buffers.Binary;

public sealed class Leb128Tests : Test
{
    private static void EncodeDecode<T>(ReadOnlySpan<T> values)
        where T : struct, IBinaryInteger<T>
    {
        Span<byte> buffer = stackalloc byte[Leb128<T>.MaxSizeInBytes];
        var writer = new SpanWriter<byte>(buffer);
        var reader = new SpanReader<byte>(buffer);

        foreach (var expected in values)
        {
            writer.Reset();
            reader.Reset();

            True(writer.WriteLeb128(expected) > 0);
            Equal(expected, reader.ReadLeb128<T>());
        }
    }

    [Fact]
    public static void EncodeDecodeInt32() => EncodeDecode([0, int.MaxValue, int.MinValue, 0x80, -1]);
    
    [Fact]
    public static void EncodeDecodeInt64() => EncodeDecode([0L, long.MaxValue, long.MinValue, 0x80L, -1L]);

    [Fact]
    public static void EncodeDecodeUInt32() => EncodeDecode([uint.MinValue, uint.MaxValue, 0x80U]);
}
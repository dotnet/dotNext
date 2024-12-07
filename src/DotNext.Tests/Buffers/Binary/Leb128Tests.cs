using System.Numerics;

namespace DotNext.Buffers.Binary;

using static IO.StreamSource;

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

    [Fact]
    public static void EncodeDecodeEmptyBuffer()
    {
        False(Leb128<int>.TryGetBytes(42, Span<byte>.Empty, out _));
        False(Leb128<short>.TryParse(ReadOnlySpan<byte>.Empty, out _, out _));
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(100500)]
    [InlineData(int.MaxValue)]
    [InlineData(0x80)]
    [InlineData(0x40)]
    [InlineData(0x7F)]
    public static void CompatibilityWithBinaryReader(int expected)
    {
        var buffer = new byte[Leb128<int>.MaxSizeInBytes];
        using var reader = new BinaryReader(new ReadOnlyMemory<byte>(buffer).AsStream());
        True(Leb128<uint>.TryGetBytes((uint)expected, buffer, out _));
        Equal(expected, reader.Read7BitEncodedInt());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100500)]
    [InlineData(int.MaxValue)]
    [InlineData(0x80)]
    [InlineData(0x40)]
    [InlineData(0x7F)]
    public static void CompatibilityWithBinaryWriter(int expected)
    {
        using var stream = new MemoryStream(Leb128<int>.MaxSizeInBytes);
        using var writer = new BinaryWriter(stream);
        writer.Write7BitEncodedInt(expected);
        
        True(Leb128<uint>.TryParse(stream.GetBuffer(), out var actual, out _));
        Equal((uint)expected, actual);
    }

    [Fact]
    public static void DifferenceBetweenSignedAndUnsignedEncoding()
    {
        Equal(Leb128<int>.MaxSizeInBytes, Leb128<uint>.MaxSizeInBytes);
        
        Span<byte> buffer = stackalloc byte[Leb128<int>.MaxSizeInBytes];
        True(Leb128<uint>.TryGetBytes(0x7Fu, buffer, out var bytesWritten));
        Equal(1, bytesWritten);
        
        True(Leb128<int>.TryGetBytes(0x7F, buffer, out bytesWritten));
        Equal(2, bytesWritten);
    }
}
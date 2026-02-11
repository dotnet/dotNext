using System.Numerics;
using DotNext.IO;

namespace DotNext.Buffers.Binary;

public sealed class Leb128Tests : Test
{
    private static void EncodeDecode<T>(ReadOnlySpan<T> values)
        where T : struct, IBinaryInteger<T>
    {
        Span<byte> buffer = stackalloc byte[Leb128<T>.MaxSizeInBytes];

        foreach (var expected in values)
        {
            True(expected.TryWriteLeb128(buffer, out var bytesWritten));
            True(Leb128.TryReadLeb128<T>(buffer, out var actual, out var bytesConsumed));
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
        False(42.TryWriteLeb128(Span<byte>.Empty, out _));
        False(short.TryReadLeb128(ReadOnlySpan<byte>.Empty, out _, out _));
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
        using var reader = new BinaryReader(Stream.Create(new ReadOnlyMemory<byte>(buffer)));
        True(((uint)expected).TryWriteLeb128(buffer, out _));
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
        
        True(uint.TryReadLeb128(stream.GetBuffer(), out var actual, out _));
        Equal((uint)expected, actual);
    }

    [Fact]
    public static void DifferenceBetweenSignedAndUnsignedEncoding()
    {
        Equal(Leb128<int>.MaxSizeInBytes, Leb128<uint>.MaxSizeInBytes);
        
        Span<byte> buffer = stackalloc byte[Leb128<int>.MaxSizeInBytes];
        True(0x7Fu.TryWriteLeb128(buffer, out var bytesWritten));
        Equal(1, bytesWritten);
        
        True(0x7F.TryWriteLeb128(buffer, out bytesWritten));
        Equal(2, bytesWritten);
    }
    
    [Fact]
    public static void MaxSizeInBytes()
    {
        Equal(sizeof(uint) + 1, Leb128<uint>.MaxSizeInBytes);
        Equal(sizeof(ulong) + 2, Leb128<ulong>.MaxSizeInBytes);
        Equal(16 + 3, Leb128<UInt128>.MaxSizeInBytes);
    }
}
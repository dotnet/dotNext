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
    [InlineData(-1)]
    [InlineData(int.MinValue)]
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
    [InlineData(-1)]
    [InlineData(int.MinValue)]
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

    [Fact]
    public static void EncodeDecodeSmallTypes()
    {
        for (int expected = short.MinValue; expected <= ushort.MaxValue; expected++)
        {
            if (expected <= short.MaxValue)
                EncodeDecode([(short)expected]);

            if (expected >= 0)
                EncodeDecode([(ushort)expected]);

            if (expected is >= sbyte.MinValue and <= sbyte.MaxValue)
                EncodeDecode([(sbyte)expected]);

            if (expected is >= byte.MinValue and <= byte.MaxValue)
                EncodeDecode([(byte)expected]);
        }
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x1F })] // bit 32 is set
    [InlineData(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x7F })]
    [InlineData(new byte[] { 0x80, 0x80, 0x80, 0x80, 0x8F, 0x00 })] // carry bit in the last possible octet
    [InlineData(new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x00 })]
    public static void DecodeUnsignedOverflow(byte[] input)
    {
        Throws<InvalidDataException>(() => uint.TryReadLeb128(input, out _, out _));
    }

    [Theory]
    [InlineData(new byte[] { 0x80, 0x80, 0x80, 0x80, 0x70 })] // sign bit is not set, but extra bits are set
    [InlineData(new byte[] { 0x80, 0x80, 0x80, 0x80, 0x08 })] // sign bit is set, but extra bits are not
    [InlineData(new byte[] { 0x80, 0x80, 0x80, 0x80, 0x10 })]
    [InlineData(new byte[] { 0x80, 0x80, 0x80, 0x80, 0xF8, 0x00 })] // carry bit in the last possible octet
    public static void DecodeSignedOverflow(byte[] input)
    {
        Throws<InvalidDataException>(() => int.TryReadLeb128(input, out _, out _));
    }

    [Fact]
    public static void DecodeLastOctetOfSmallTypes()
    {
        True(sbyte.TryReadLeb128([0x80, 0x7F], out var signed, out _));
        Equal(sbyte.MinValue, signed);
        Throws<InvalidDataException>(() => sbyte.TryReadLeb128([0x80, 0x7E], out _, out _));
        Throws<InvalidDataException>(() => sbyte.TryReadLeb128([0x80, 0x01], out _, out _));

        True(byte.TryReadLeb128([0xFF, 0x01], out var unsigned, out _));
        Equal(byte.MaxValue, unsigned);
        Throws<InvalidDataException>(() => byte.TryReadLeb128([0xFF, 0x03], out _, out _));
    }

    [Fact]
    public static void DecodeIncompleteInput()
    {
        False(uint.TryReadLeb128([0xFF, 0xFF, 0xFF, 0xFF], out _, out var bytesConsumed));
        Equal(4, bytesConsumed);
    }
}

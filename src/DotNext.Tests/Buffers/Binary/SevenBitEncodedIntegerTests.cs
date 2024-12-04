using System.Numerics;

namespace DotNext.Buffers.Binary;

public sealed class SevenBitEncodedIntegerTests : Test
{
    private static void EncodeDecodeZeroAndMaxValue<T>()
        where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>
    {
        Span<byte> buffer = stackalloc byte[SevenBitEncodedInteger<T>.MaxSizeInBytes];
        var writer = new SpanWriter<byte>(buffer);
        var reader = new SpanReader<byte>(buffer);
        
        Equal(1, writer.Write7BitEncodedInteger(T.Zero));
        Equal(T.Zero, reader.Read7BitEncodedInteger<T>());
        
        writer.Reset();
        reader.Reset();

        Equal(SevenBitEncodedInteger<T>.MaxSizeInBytes, writer.Write7BitEncodedInteger(T.AllBitsSet));
        Equal(T.AllBitsSet, reader.Read7BitEncodedInteger<T>());
    }

    [Fact]
    public static void EncodeDecodeUInt32() => EncodeDecodeZeroAndMaxValue<uint>();
    
    [Fact]
    public static void EncodeDecodeUInt64() => EncodeDecodeZeroAndMaxValue<ulong>();
}
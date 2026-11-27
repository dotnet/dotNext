using System.Buffers;
using System.IO.Pipelines;
using System.Numerics;
using System.Text;

namespace DotNext.Buffers;

using Binary;
using IO;
using static IO.Pipelines.PipeExtensions;

public sealed class SequenceReaderTests : Test
{
    [Fact]
    public static async Task ReadMemory()
    {
        var sequence = ToReadOnlySequence<byte>(new byte[] { 1, 5, 8, 9 }, 2);
        False(sequence.IsSingleSegment);
        var result = new byte[3];
        IAsyncBinaryReader reader = new SequenceReader(sequence);
        await reader.ReadAsync(result, TestToken);
        Equal(1, result[0]);
        Equal(5, result[1]);
        Equal(8, result[2]);
    }

    [Fact]
    public static async Task CopyToStream()
    {
        var content = new byte[] { 1, 5, 8, 9 };
        IAsyncBinaryReader reader = new SequenceReader(ToReadOnlySequence<byte>(content, 2));
        using var ms = new MemoryStream();
        await reader.CopyToAsync(ms, token: TestToken);
        ms.Position = 0;
        Equal(content, ms.ToArray());
    }

    [Fact]
    public static async Task CopyToPipe()
    {
        var expected = new byte[] { 1, 5, 8, 9 };
        IAsyncBinaryReader reader = new SequenceReader(ToReadOnlySequence<byte>(expected, 2));
        var pipe = new Pipe();
        await reader.CopyToAsync(pipe.Writer, token: TestToken);
        await pipe.Writer.CompleteAsync();
        var actual = new byte[expected.Length];
        await pipe.Reader.ReadExactlyAsync(actual, TestToken);
        Equal(expected, actual);
    }

    [Fact]
    public static void ReadBlittableType()
    {
        var writer = new ArrayBufferWriter<byte>();
        writer.Write<Blittable<decimal>>(new() { Value = 10M });
        writer.WriteLittleEndian(42L);
        writer.WriteLittleEndian(43UL);
        writer.WriteLittleEndian(44);
        writer.WriteBigEndian(45U);
        writer.WriteBigEndian<short>(46);
        writer.WriteBigEndian<ushort>(47);

        var reader = new SequenceReader(writer.WrittenMemory);
        Equal(10M, reader.Read<Blittable<decimal>>().Value);
        Equal(42L, reader.ReadLittleEndian<long>());
        Equal(43UL, reader.ReadLittleEndian<ulong>());
        Equal(44, reader.ReadLittleEndian<int>());
        Equal(45U, reader.ReadBigEndian<uint>());
        Equal(46, reader.ReadBigEndian<short>());
        Equal(47, reader.ReadBigEndian<ushort>());
    }

    [Fact]
    public static void ReadBlock()
    {
        var reader = new SequenceReader(new byte[] { 10, 20, 30, 40, 50, 60 });

        var block = new byte[3];
        reader.Read(block.AsSpan());

        Equal(10, block[0]);
        Equal(20, block[1]);
        Equal(30, block[2]);

        reader.Read(block.AsSpan());
        Equal(40, block[0]);
        Equal(50, block[1]);
        Equal(60, block[2]);

        Throws<EndOfStreamException>(() => reader.Read(block.AsSpan()));
    }

    private static async Task ReadWriteStringUsingEncodingAsync(string value, Encoding encoding, LengthFormat lengthEnc)
    {
        Memory<byte> buffer = new byte[16];
        using var ms = new MemoryStream();
        await ms.EncodeAsync(value.AsMemory(), encoding, lengthEnc, buffer);
        ms.Position = 0;
        IAsyncBinaryReader reader = new SequenceReader(ms.ToArray());
        True(reader.TryGetRemainingBytesCount(out long remainingCount));
        Equal(ms.Length, remainingCount);
        using var result = await reader.DecodeAsync(encoding, lengthEnc);
        Equal(value, result.ToString());
    }

    [Theory]
    [InlineData(LengthFormat.Compressed)]
    [InlineData(LengthFormat.LittleEndian)]
    [InlineData(LengthFormat.BigEndian)]
    public static async Task ReadWriteBufferedStringAsync(LengthFormat lengthEnc)
    {
        const string testString1 = "Hello, world!&*(@&*(fghjwgfwffgw";
        await ReadWriteStringUsingEncodingAsync(testString1, Encoding.UTF8, lengthEnc);
        await ReadWriteStringUsingEncodingAsync(testString1, Encoding.Unicode, lengthEnc);
        await ReadWriteStringUsingEncodingAsync(testString1, Encoding.UTF32, lengthEnc);
        await ReadWriteStringUsingEncodingAsync(testString1, Encoding.ASCII, lengthEnc);
        const string testString2 = "������, ���!";
        await ReadWriteStringUsingEncodingAsync(testString2, Encoding.UTF8, lengthEnc);
        await ReadWriteStringUsingEncodingAsync(testString2, Encoding.Unicode, lengthEnc);
        await ReadWriteStringUsingEncodingAsync(testString2, Encoding.UTF32, lengthEnc);
    }

    [Theory]
    [InlineData("UTF-8")]
    [InlineData("UTF-16LE")]
    [InlineData("UTF-16BE")]
    [InlineData("UTF-32LE")]
    [InlineData("UTF-32BE")]
    public static void EncodeDecodeUsingEnumerator(string encodingName)
    {
        var encoding = Encoding.GetEncoding(encodingName);

        const string value = "Hello, world!&*(@&*(fghjwgfwffgw Привет, мир!";
        var writer = new ArrayBufferWriter<byte>();
        writer.Encode(value, encoding, LengthFormat.Compressed);

        var reader = new SequenceReader(writer.WrittenMemory);
        Span<char> charBuffer = stackalloc char[9];

        var bufferWriter = new ArrayBufferWriter<char>(value.Length);
        foreach (var chunk in reader.Decode(encoding, LengthFormat.Compressed, charBuffer))
            bufferWriter.Write(chunk);

        Equal(value, bufferWriter.WrittenSpan.ToString());
    }

    [Fact]
    public static void EmptyReader()
    {
        True(default(SequenceReader).IsEmpty);
    }

    [Fact]
    public static void TryRead()
    {
        ReadOnlyMemory<byte> expected = new byte[] { 1, 2, 3 };
        var reader = new SequenceReader(expected);
        False(reader.IsEmpty);

        True(reader.TryRead(out var actual));
        Equal(expected.Span, actual.Span);
        True(reader.IsEmpty);
        False(reader.TryRead(out actual));
    }
    
    [Fact]
    public static void TryRead2()
    {
        ReadOnlyMemory<byte> expected = new byte[] { 1, 2, 3 };
        var reader = new SequenceReader(expected);
        False(reader.IsEmpty);

        True(reader.TryRead(4, out var actual));
        Equal(expected.Span, actual.Span);
        True(reader.IsEmpty);
        False(reader.TryRead(4, out actual));
    }

    [Fact]
    public static void ReadWriteBigInteger()
    {
        var expected = (BigInteger)100500;
        var writer = new ArrayBufferWriter<byte>();

        Equal(3, writer.Write(expected));

        var reader = new SequenceReader(writer.WrittenMemory);
        Equal(expected, new BigInteger(reader.ReadToEnd().FirstSpan));
    }

    [Fact]
    public static void ReadBytes()
    {
        var reader = new SequenceReader(new byte[] { 42, byte.MaxValue });
        Equal(42, reader.ReadByte());
        Equal(byte.MaxValue, reader.ReadByte());
        
        var exceptionThrown = false;
        try
        {
            reader.ReadByte();
        }
        catch (EndOfStreamException)
        {
            exceptionThrown = true;
        }

        True(exceptionThrown);
    }

    [Fact]
    public static void UseBufferReader()
    {
        var reader = new SequenceReader(new byte[] { 42, 43 });
        
        var parser = new ByteParser();
        reader.Read(ref parser);
        Equal(42, parser.Value);

        parser = default;
        reader.Read(ref parser);
        Equal(43, parser.Value);

        var exceptionThrown = false;
        try
        {
            parser = default;
            reader.Read(ref parser);
        }
        catch (EndOfStreamException)
        {
            exceptionThrown = true;
        }

        True(exceptionThrown);
    }

    private ref struct ByteParser : IBufferReader
    {
        public byte Value { get; private set; }
        private bool written;

        void IBufferReader.Apply(scoped ReadOnlySpan<byte> buffer)
        {
            Value = buffer[0];
            written = true;
        }

        readonly int IBufferReader.RemainingBytes => written ? 0 : 1;

        static bool IBufferReader.ThrowOnPartialData => true;
    }

    [Fact]
    public static void NegativeLength()
    {
        var bytes = new byte[sizeof(int) + 5];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes, -5);

        Throws<InvalidDataException>(() => new SequenceReader(bytes).ReadBlock(LengthFormat.LittleEndian, allocator: null));
        Throws<InvalidDataException>(() => new SequenceReader(bytes).ReadBlock(LengthFormat.LittleEndian));
        Throws<InvalidDataException>(() => new SequenceReader(bytes).Decode(Encoding.UTF8, LengthFormat.LittleEndian));
        Throws<InvalidDataException>(() => new SequenceReader(bytes).Parse<int>(LengthFormat.LittleEndian));
    }

    // a😀bcd: the 4-byte char split 3 + 1 between chunks produces a surrogate pair from 1 byte;
    // abc?defg: the dangling lead byte produces a replacement char in addition to the next chunk
    public static TheoryData<byte[]> ChunkBoundaryPayloads => new()
    {
        new byte[] { 0x61, 0xF0, 0x9F, 0x98, 0x80, 0x62, 0x63, 0x64 },
        new byte[] { 0x61, 0x62, 0x63, 0xE2, 0x64, 0x65, 0x66, 0x67 },
    };

    private static byte[] WithLengthPrefix(byte[] payload)
    {
        var result = new byte[sizeof(int) + payload.Length];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(result, payload.Length);
        payload.CopyTo(result, sizeof(int));
        return result;
    }

    [Theory]
    [MemberData(nameof(ChunkBoundaryPayloads))]
    public static void DecodeInChunksAtCharBoundary(byte[] payload)
    {
        var expected = Encoding.UTF8.GetString(payload);

        var actual = new StringBuilder();
        foreach (var chunk in new SequenceReader(WithLengthPrefix(payload)).Decode(Encoding.UTF8, LengthFormat.LittleEndian, new char[4].AsMemory()))
            actual.Append(chunk.Span);

        Equal(expected, actual.ToString());

        actual.Clear();
        var reader = new SequenceReader(WithLengthPrefix(payload));
        foreach (var chunk in reader.Decode(Encoding.UTF8, LengthFormat.LittleEndian, stackalloc char[4]))
            actual.Append(chunk);

        Equal(expected, actual.ToString());

        Throws<ArgumentException>(() => new SequenceReader(WithLengthPrefix(payload)).Decode(Encoding.UTF8, LengthFormat.LittleEndian, new char[1].AsMemory()));
    }

    [Fact]
    public static void DecodeInChunksWithSegmentedChar()
    {
        // the segment containing only the leading byte of the 3-byte char produces no chars
        var bytes = WithLengthPrefix("a€b"u8.ToArray());
        var actual = new StringBuilder();
        foreach (var chunk in new SequenceReader(ToReadOnlySequence<byte>(bytes, 1)).Decode(Encoding.UTF8, LengthFormat.LittleEndian, new char[4].AsMemory()))
            actual.Append(chunk.Span);

        Equal("a€b", actual.ToString());
    }
}
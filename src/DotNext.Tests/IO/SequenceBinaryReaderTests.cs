using System.Buffers;
using System.IO.Pipelines;
using System.Text;

namespace DotNext.IO;

using Buffers;
using Buffers.Binary;
using static Pipelines.PipeExtensions;

public sealed class SequenceBinaryReaderTests : Test
{
    [Fact]
    public static async Task ReadMemory()
    {
        var sequence = ToReadOnlySequence<byte>(new byte[] { 1, 5, 8, 9 }, 2);
        False(sequence.IsSingleSegment);
        var result = new byte[3];
        IAsyncBinaryReader reader = IAsyncBinaryReader.Create(sequence);
        await reader.ReadAsync(result);
        Equal(1, result[0]);
        Equal(5, result[1]);
        Equal(8, result[2]);
    }

    [Fact]
    public static async Task CopyToStream()
    {
        var content = new byte[] { 1, 5, 8, 9 };
        IAsyncBinaryReader reader = IAsyncBinaryReader.Create(ToReadOnlySequence<byte>(content, 2));
        using var ms = new MemoryStream();
        await reader.CopyToAsync(ms);
        ms.Position = 0;
        Equal(content, ms.ToArray());
    }

    [Fact]
    public static async Task CopyToPipe()
    {
        var expected = new byte[] { 1, 5, 8, 9 };
        IAsyncBinaryReader reader = IAsyncBinaryReader.Create(ToReadOnlySequence<byte>(expected, 2));
        var pipe = new Pipe();
        await reader.CopyToAsync(pipe.Writer);
        await pipe.Writer.CompleteAsync();
        var actual = new byte[expected.Length];
        await pipe.Reader.ReadExactlyAsync(actual);
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

        var reader = IAsyncBinaryReader.Create(writer.WrittenMemory);
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
        var reader = IAsyncBinaryReader.Create(new byte[] { 10, 20, 30, 40, 50, 60 });

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
        IAsyncBinaryReader reader = IAsyncBinaryReader.Create(ms.ToArray());
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

        var reader = IAsyncBinaryReader.Create(writer.WrittenMemory);
        Span<char> charBuffer = stackalloc char[9];

        var bufferWriter = new ArrayBufferWriter<char>(value.Length);
        foreach (var chunk in reader.Decode(encoding, LengthFormat.Compressed, charBuffer))
            bufferWriter.Write(chunk);

        Equal(value, bufferWriter.WrittenSpan.ToString());
    }
}
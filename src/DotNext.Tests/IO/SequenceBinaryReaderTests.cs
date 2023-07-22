using System.Buffers;
using System.IO.Pipelines;
using System.Text;

namespace DotNext.IO;

using Buffers;
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
        await pipe.Reader.ReadBlockAsync(actual);
        Equal(expected, actual);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public static void ReadBlittableType(bool littleEndian)
    {
        var writer = new ArrayBufferWriter<byte>();
        writer.Write(10M);
        writer.WriteInt64(42L, littleEndian);
        writer.WriteUInt64(43UL, littleEndian);
        writer.WriteInt32(44, littleEndian);
        writer.WriteUInt32(45U, littleEndian);
        writer.WriteInt16(46, littleEndian);
        writer.WriteUInt16(47, littleEndian);

        var reader = IAsyncBinaryReader.Create(writer.WrittenMemory);
        Equal(10M, reader.Read<decimal>());
        Equal(42L, reader.ReadInt64(littleEndian));
        Equal(43UL, reader.ReadUInt64(littleEndian));
        Equal(44, reader.ReadInt32(littleEndian));
        Equal(45U, reader.ReadUInt32(littleEndian));
        Equal(46, reader.ReadInt16(littleEndian));
        Equal(47, reader.ReadUInt16(littleEndian));
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

    private static async Task ReadWriteStringUsingEncodingAsync(string value, Encoding encoding, LengthFormat? lengthEnc)
    {
        using var ms = new MemoryStream();
        await ms.WriteStringAsync(value.AsMemory(), encoding, lengthEnc);
        ms.Position = 0;
        IAsyncBinaryReader reader = IAsyncBinaryReader.Create(ms.ToArray());
        True(reader.TryGetRemainingBytesCount(out long remainingCount));
        Equal(ms.Length, remainingCount);
        var result = await (lengthEnc is null ?
            reader.ReadStringAsync(encoding.GetByteCount(value), encoding) :
            reader.ReadStringAsync(lengthEnc.GetValueOrDefault(), encoding));
        Equal(value, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(LengthFormat.Compressed)]
    [InlineData(LengthFormat.Plain)]
    [InlineData(LengthFormat.PlainLittleEndian)]
    [InlineData(LengthFormat.PlainBigEndian)]
    public static async Task ReadWriteBufferedStringAsync(LengthFormat? lengthEnc)
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
}
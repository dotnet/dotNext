using System.Buffers;
using System.Text;

namespace DotNext.IO;

using Buffers.Binary;
using Collections.Generic;

public sealed class StreamExtensionsTests : Test
{
    private static async Task ReadWriteStringUsingEncodingAsync(string value, Encoding encoding, int bufferSize, LengthFormat lengthEnc)
    {
        Memory<byte> buffer = new byte[16];
        using var ms = new MemoryStream();
        await ms.EncodeAsync(value.AsMemory(), encoding, lengthEnc, buffer);
        ms.Position = 0;
        var reader = IAsyncBinaryReader.Create(ms, buffer);
        True(reader.TryGetRemainingBytesCount(out long remainingCount));
        Equal(ms.Length, remainingCount);

        using var result = await reader.DecodeAsync(encoding, lengthEnc);
        Equal(value, result.ToString());
    }

    private static async Task ReadWriteStringUsingEncodingAsync(string value, Encoding encoding, LengthFormat lengthEnc)
    {
        Memory<byte> buffer = new byte[16];
        using var ms = new MemoryStream();
        await ms.EncodeAsync(value.AsMemory(), encoding, lengthEnc, buffer);
        ms.Position = 0;
        using var result = await ms.DecodeAsync(encoding, lengthEnc, buffer);
        Equal(value, result.ToString());
    }

    [Theory]
    [InlineData(LengthFormat.Compressed)]
    [InlineData(LengthFormat.LittleEndian)]
    [InlineData(LengthFormat.BigEndian)]
    public static async Task ReadWriteStringAsync(LengthFormat lengthEnc)
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
    [InlineData(10, LengthFormat.Compressed)]
    [InlineData(15, LengthFormat.Compressed)]
    [InlineData(1024, LengthFormat.Compressed)]
    [InlineData(10, LengthFormat.LittleEndian)]
    [InlineData(15, LengthFormat.LittleEndian)]
    [InlineData(1024, LengthFormat.LittleEndian)]
    [InlineData(10, LengthFormat.BigEndian)]
    [InlineData(15, LengthFormat.BigEndian)]
    [InlineData(1024, LengthFormat.BigEndian)]
    public static async Task ReadWriteBufferedStringAsync(int bufferSize, LengthFormat lengthEnc)
    {
        const string testString1 = "Hello, world!&*(@&*(fghjwgfwffgw";
        await ReadWriteStringUsingEncodingAsync(testString1, Encoding.UTF8, bufferSize, lengthEnc);
        await ReadWriteStringUsingEncodingAsync(testString1, Encoding.Unicode, bufferSize, lengthEnc);
        await ReadWriteStringUsingEncodingAsync(testString1, Encoding.UTF32, bufferSize, lengthEnc);
        await ReadWriteStringUsingEncodingAsync(testString1, Encoding.ASCII, bufferSize, lengthEnc);
        const string testString2 = "������, ���!";
        await ReadWriteStringUsingEncodingAsync(testString2, Encoding.UTF8, bufferSize, lengthEnc);
        await ReadWriteStringUsingEncodingAsync(testString2, Encoding.Unicode, bufferSize, lengthEnc);
        await ReadWriteStringUsingEncodingAsync(testString2, Encoding.UTF32, bufferSize, lengthEnc);
    }

    [Fact]
    public static async Task ReadWriteMemoryUsingReader()
    {
        using var ms = new MemoryStream();
        ms.Write([1, 5, 7, 9]);
        ms.Position = 0;
        var reader = IAsyncBinaryReader.Create(ms, new byte[128]);
        var memory = new byte[4];
        await reader.ReadAsync(memory);
        Equal(1, memory[0]);
        Equal(5, memory[1]);
        Equal(7, memory[2]);
        Equal(9, memory[3]);
    }

    [Fact]
    public static async Task ReadWriteBlittableTypeAsync()
    {
        Memory<byte> buffer = new byte[16];
        using var ms = new MemoryStream();
        await ms.WriteAsync<Blittable<decimal>>(new() { Value = 10M }, buffer);
        ms.Position = 0;
        Equal(10M, (await ms.ReadAsync<Blittable<decimal>>(buffer)).Value);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(16)]
    [InlineData(0x7F)]
    [InlineData(0x80)]
    public static async Task BinaryReaderInterop(int length)
    {
        var expected = Random.Shared.NextString(Alphabet + AlphabetUpperCase + Numbers, length);
        using var ms = new MemoryStream();
        await ms.EncodeAsync(expected.AsMemory(), Encoding.UTF8, LengthFormat.Compressed, new byte[16]);
        ms.Position = 0;
        using var reader = new BinaryReader(ms, Encoding.UTF8, true);
        Equal(expected, reader.ReadString());
    }

    [Theory]
    [InlineData(7)]
    [InlineData(16)]
    [InlineData(0x7F)]
    [InlineData(0x80)]
    public static async Task BinaryWriterInterop(int length)
    {
        var expected = Random.Shared.NextString(Alphabet + AlphabetUpperCase + Numbers, length);
        using var ms = new MemoryStream();
        await using (var writer = new BinaryWriter(ms, Encoding.UTF8, true))
        {
            writer.Write(expected);
        }
        ms.Position = 0;

        using var result = await ms.DecodeAsync(Encoding.UTF8, LengthFormat.Compressed, new byte[16]);
        Equal(expected, result.Span);
    }

    [Fact]
    public static void CombineStreams()
    {
        using var ms1 = new MemoryStream([1, 2, 3]);
        using var ms2 = new MemoryStream([4, 5, 6]);
        using var combined = ms1.Combine([ms2]);
        True(combined.CanRead);
        False(combined.CanWrite);
        False(combined.CanSeek);

        Span<byte> buffer = stackalloc byte[6];
        combined.ReadExactly(buffer);

        Equal([1, 2, 3, 4, 5, 6], buffer.ToArray());
    }

    [Fact]
    public static async Task CombineStreamsAsync()
    {
        await using var ms1 = new MemoryStream([1, 2, 3]);
        await using var ms2 = new MemoryStream([4, 5, 6]);
        await using var combined = StreamExtensions.Combine([ms1, ms2]);

        var buffer = new byte[6];
        await combined.ReadExactlyAsync(buffer);

        Equal([1, 2, 3, 4, 5, 6], buffer);
    }

    [Fact]
    public static void CopyCombinedStreams()
    {
        using var ms1 = new MemoryStream([1, 2, 3]);
        using var ms2 = new MemoryStream([4, 5, 6]);
        using var combined = List.Singleton(ms1).Append(ms2).Combine();
        using var result = new MemoryStream();

        combined.CopyTo(result, 128);
        Equal([1, 2, 3, 4, 5, 6], result.ToArray());
    }

    [Fact]
    public static async Task CopyCombinedStreamsAsync()
    {
        await using var ms1 = new MemoryStream([1, 2, 3]);
        await using var ms2 = new MemoryStream([4, 5, 6]);
        await using var combined = ms1.Combine([ms2]);
        await using var result = new MemoryStream();

        await combined.CopyToAsync(result, 128);
        Equal([1, 2, 3, 4, 5, 6], result.ToArray());
    }

    [Fact]
    public static void ReadBytesFromCombinedStream()
    {
        using var ms1 = new MemoryStream([1, 2, 3]);
        using var ms2 = new MemoryStream([4, 5, 6]);
        using var combined = ms1.Combine([ms2]);

        Equal(1, combined.ReadByte());
        Equal(2, combined.ReadByte());
        Equal(3, combined.ReadByte());
        Equal(4, combined.ReadByte());
        Equal(5, combined.ReadByte());
        Equal(6, combined.ReadByte());
        Equal(-1, combined.ReadByte());
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public static void CombineManyStreams(byte streamCount)
    {
        using var stream = GetStreams(streamCount).Combine(leaveOpen: false);
        Equal(streamCount, stream.Length);
        var actual = new byte[streamCount];
        stream.ReadExactly(actual);
        var expected = Set.Range<byte, EnclosedEndpoint<byte>, DisclosedEndpoint<byte>>(0, streamCount);
        Equal(expected, actual);
        
        static IEnumerable<Stream> GetStreams(byte streamCount)
        {
            for (var i = 0; i < streamCount; i++)
            {
                var ms = new MemoryStream();
                ms.WriteByte((byte)i);
                ms.Seek(0L, SeekOrigin.Begin);
                yield return ms;
            }
        }
    }

    [Fact]
    public static async Task UnsupportedMethodsOfSparseStream()
    {
        await using var ms1 = new MemoryStream([1, 2, 3]);
        await using var ms2 = new MemoryStream([4, 5, 6]);
        await using var combined = ms1.Combine([ms2]);

        Throws<NotSupportedException>(() => combined.SetLength(0L));
        Throws<NotSupportedException>(() => combined.Seek(0L, default));
        Throws<NotSupportedException>(() => combined.Position.ToString());
        Throws<NotSupportedException>(() => combined.Position = 42L);
        Throws<NotSupportedException>(() => combined.WriteByte(1));
        Throws<NotSupportedException>(() => combined.Write(ReadOnlySpan<byte>.Empty));
        await ThrowsAsync<NotSupportedException>(async () => await combined.WriteAsync(ReadOnlyMemory<byte>.Empty));
    }

    [Fact]
    public static async Task ReadEmptyStream()
    {
        var count = 0;

        await foreach (var chunk in Stream.Null.ReadAllAsync(64))
            count++;

        Equal(0, count);
    }

    [Fact]
    public static async Task ReadEntireStream()
    {
        var bytes = RandomBytes(1024);
        using var source = new MemoryStream(bytes, false);
        using var destination = new MemoryStream(1024);

        await foreach (var chunk in source.ReadAllAsync(64))
        {
            await destination.WriteAsync(chunk);
        }

        Equal(bytes, destination.ToArray());
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    public static async Task DecodeNullTerminatedStringAsync(int bufferSize)
    {
        using var ms = new MemoryStream();
        ms.Write("Привет, \u263A!"u8);
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.Position = 0L;

        var writer = new ArrayBufferWriter<char>();
        await ms.ReadUtf8Async(new byte[bufferSize], writer);
        Equal("Привет, \u263A!", writer.WrittenSpan.ToString());
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    public static async Task DecodeNullTerminatedString2Async(int bufferSize)
    {
        using var ms = new MemoryStream();
        ms.Write("Привет, \u263A!"u8);
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.Position = 0L;

        var writer = new StringBuilder();
        await ms.ReadUtf8Async(new byte[bufferSize], new char[Encoding.UTF8.GetMaxCharCount(bufferSize)], Write, writer);
        Equal("Привет, \u263A!", writer.ToString());

        static ValueTask Write(ReadOnlyMemory<char> input, StringBuilder output, CancellationToken token)
        {
            output.Append(input.Span);
            return ValueTask.CompletedTask;
        }
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    public static void DecodeNullTerminatedString(int bufferSize)
    {
        using var ms = new MemoryStream();
        ms.Write("Привет, \u263A!"u8);
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.Position = 0L;

        var writer = new ArrayBufferWriter<char>();
        ms.ReadUtf8(stackalloc byte[bufferSize], writer);
        Equal("Привет, \u263A!", writer.WrittenSpan.ToString());
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    public static void DecodeNullTerminatedString2(int bufferSize)
    {
        using var ms = new MemoryStream();
        ms.Write("Привет, \u263A!"u8);
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.Position = 0L;

        var writer = new StringBuilder();
        ms.ReadUtf8(stackalloc byte[bufferSize], new char[Encoding.UTF8.GetMaxCharCount(bufferSize)], Write, writer);
        Equal("Привет, \u263A!", writer.ToString());

        static void Write(ReadOnlySpan<char> input, StringBuilder output)
            => output.Append(input);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    public static void DecodeNullTerminatedStringNoTrailinigZeroByte(int bufferSize)
    {
        using var ms = new MemoryStream();
        ms.Write("Привет, \u263A!"u8);
        ms.Position = 0L;

        var writer = new ArrayBufferWriter<char>();
        ms.ReadUtf8(stackalloc byte[bufferSize], writer);
        Equal("Привет, \u263A!", writer.WrittenSpan.ToString());
    }

    [Fact]
    public static void DecodeNullTerminatedEmptyString()
    {
        using var ms = new MemoryStream();
        ms.Write("\0"u8);
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.Position = 0L;

        var writer = new ArrayBufferWriter<char>();
        ms.ReadUtf8(stackalloc byte[8], writer);
        Equal(string.Empty, writer.WrittenSpan.ToString());
    }
    
    [Fact]
    public static async Task ReadBlockAsSequenceAsync()
    {
        var bytes = RandomBytes(1024);
        using var source = new MemoryStream(bytes, false);
        using var destination = new MemoryStream(1024);

        await foreach (var chunk in source.ReadExactlyAsync(512L, 64))
        {
            await destination.WriteAsync(chunk);
        }

        Equal(new ReadOnlySpan<byte>(bytes, 0, 512), destination.ToArray());
    }
}
using System.Text;

namespace DotNext.IO;

public sealed class MemorySegmentStreamTests : Test
{
    private static async Task ReadWriteStringUsingEncodingAsync(string value, Encoding encoding, LengthFormat lengthEnc)
    {
        Memory<byte> buffer = new byte[16];
        await using var ms = new MemorySegmentStream(new byte[1024]);
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
    [InlineData(false)]
    [InlineData(true)]
    public static void Overflow(bool skipOnOverflow)
    {
        using var stream = new MemorySegmentStream(new byte[128]) { SkipOnOverflow = skipOnOverflow };
        ReadOnlyMemory<byte> dataToWrite = RandomBytes(256);

        if (skipOnOverflow)
        {
            stream.Write(dataToWrite.Span);
            Equal(stream.ConsumedSpan, dataToWrite.Span.Slice(0, (int)stream.Length));
            True(stream.RemainingSpan.IsEmpty);
        }
        else
        {
            Throws<IOException>(() => stream.Write(dataToWrite.Span));
        }
    }

    [Fact]
    public static void ReadWrite()
    {
        const int bufferSize = 128;
        Memory<byte> buffer = new byte[bufferSize];
        using var stream = new MemorySegmentStream(buffer);
        True(stream.ConsumedSpan.IsEmpty);
        False(stream.RemainingSpan.IsEmpty);
        
        ReadOnlySpan<byte> dataToWrite = RandomBytes(bufferSize);
        stream.Write(dataToWrite);
        True(stream.RemainingSpan.IsEmpty);

        Equal(dataToWrite, stream.ConsumedSpan);

        Span<byte> readBuffer = new byte[buffer.Length];
        stream.Position = 0L;
        Equal(buffer.Length, stream.Read(readBuffer));
        Equal(dataToWrite, readBuffer);
    }

    [Fact]
    public static async Task ReadWriteAsync()
    {
        const int bufferSize = 128;
        Memory<byte> buffer = new byte[bufferSize];
        await using var stream = new MemorySegmentStream(buffer);
        True(stream.ConsumedSpan.IsEmpty);
        False(stream.RemainingSpan.IsEmpty);

        ReadOnlyMemory<byte> dataToWrite = RandomBytes(bufferSize);
        await stream.WriteAsync(dataToWrite);
        True(stream.RemainingSpan.IsEmpty);

        Equal(dataToWrite.Span, stream.ConsumedSpan);

        Memory<byte> readBuffer = new byte[buffer.Length];
        stream.Position = 0L;
        Equal(buffer.Length, await stream.ReadAsync(readBuffer));
        Equal(dataToWrite, readBuffer);
    }

    [Fact]
    public static void Truncate()
    {
        const int bufferSize = 128;
        Memory<byte> buffer = new byte[bufferSize];
        using var stream = new MemorySegmentStream(buffer);
        
        ReadOnlySpan<byte> dataToWrite = RandomBytes(bufferSize);
        stream.Write(dataToWrite);

        stream.SetLength(bufferSize / 2);
        Equal(bufferSize / 2, stream.Length);
        Equal(bufferSize / 2, stream.Position);
        Equal(dataToWrite.Slice(0, bufferSize / 2), stream.ConsumedSpan);
    }

    [Fact]
    public static void SetInvalidLength()
    {
        const int bufferSize = 128;
        Memory<byte> buffer = new byte[bufferSize];
        using var stream = new MemorySegmentStream(buffer);
        
        Throws<ArgumentOutOfRangeException>(() => stream.SetLength(-1L));
        Throws<IOException>(() => stream.SetLength(bufferSize + 1));
    }
}
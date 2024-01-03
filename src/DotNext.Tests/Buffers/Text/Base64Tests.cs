using System.Buffers;
using System.Text;

namespace DotNext.Buffers.Text;

using IO;

public sealed class Base64Tests : Test
{
    [Theory]
    [InlineData(17)]
    [InlineData(32)]
    [InlineData(63)]
    public static void DecodeBase64BytesToMemoryBlock(int size)
    {
        var expected = RandomBytes(size);
        ReadOnlySpan<byte> base64 = Encoding.UTF8.GetBytes(Convert.ToBase64String(expected));
        var decoder = new Base64Decoder();
        using var actual = decoder.DecodeFromUtf8(base64);
        False(decoder.NeedMoreData);

        Equal(expected, actual.Span.ToArray());
    }

    [Fact]
    public static void DecodeInvalidBlock()
    {
        var decoder = new Base64Decoder();
        using var writer = new PoolingArrayBufferWriter<byte>();
        decoder.DecodeFromUtf8("AB"u8, writer);
        True(decoder.NeedMoreData);
        Equal(0, writer.WrittenCount);
    }

    [Theory]
    [InlineData(17)]
    [InlineData(32)]
    [InlineData(63)]
    public static void DecodeBase64CharsToMemoryBlock(int size)
    {
        var expected = RandomBytes(size);
        ReadOnlySpan<char> base64 = Convert.ToBase64String(expected);
        var decoder = new Base64Decoder();
        using var actual = decoder.DecodeFromUtf16(base64);
        False(decoder.NeedMoreData);

        Equal(expected, actual.Span.ToArray());
    }

    [Theory]
    [InlineData(17)]
    [InlineData(32)]
    [InlineData(63)]
    [InlineData(512)]
    [InlineData(1024)]
    public static void EncodeToBufferWriterUtf8(int size)
    {
        var expected = RandomBytes(size);

        var encoder = new Base64Encoder();
        False(encoder.HasBufferedData);
        var writer = new ArrayBufferWriter<byte>();

        encoder.EncodeToUtf8(expected.AsSpan(0, size / 2), writer, flush: false);
        encoder.EncodeToUtf8(expected.AsSpan().Slice(size / 2), writer, flush: true);
        Equal(0, encoder.BufferedData.Length);

        var decoder = new Base64Decoder();
        using var actual = decoder.DecodeFromUtf8(writer.WrittenSpan);

        Equal(expected, actual.Span.ToArray());
    }

    [Theory]
    [InlineData(17)]
    [InlineData(32)]
    [InlineData(63)]
    [InlineData(512)]
    [InlineData(1024)]
    public static void EncodeToBufferWriterUtf16(int size)
    {
        var expected = RandomBytes(size);

        var encoder = new Base64Encoder();
        var writer = new ArrayBufferWriter<char>();

        encoder.EncodeToUtf16(expected.AsSpan(0, size / 2), writer, flush: false);
        encoder.EncodeToUtf16(expected.AsSpan().Slice(size / 2), writer, flush: true);

        var decoder = new Base64Decoder();
        using var actual = decoder.DecodeFromUtf16(writer.WrittenSpan);

        Equal(expected, actual.Span.ToArray());
    }

    [Theory]
    [InlineData(17)]
    [InlineData(32)]
    [InlineData(63)]
    [InlineData(512)]
    [InlineData(1024)]
    public static void EncodeToUtf8(int size)
    {
        var expected = RandomBytes(size);

        var encoder = new Base64Encoder();
        using var base64 = encoder.EncodeToUtf8(expected, flush: true);

        var decoder = new Base64Decoder();
        using var actual = decoder.DecodeFromUtf8(base64.Span);

        Equal(expected, actual.Span.ToArray());
    }

    [Theory]
    [InlineData(17)]
    [InlineData(32)]
    [InlineData(63)]
    [InlineData(512)]
    [InlineData(1024)]
    public static void EncodeToUtf16(int size)
    {
        var expected = RandomBytes(size);

        var encoder = new Base64Encoder();
        using var base64 = encoder.EncodeToUtf16(expected, flush: true);

        var decoder = new Base64Decoder();
        using var actual = decoder.DecodeFromUtf16(base64.Span);

        Equal(expected, actual.Span.ToArray());
    }

    [Fact]
    public static void FlushToBytes()
    {
        var encoder = new Base64Encoder();
        var writer = new ArrayBufferWriter<byte>();
        byte[] expected = [1, 2];

        encoder.EncodeToUtf8(expected, writer, flush: false);
        True(encoder.HasBufferedData);

        Equal(2, encoder.BufferedData.Length);
        Equal(expected, encoder.BufferedData.Slice(0, 2).ToArray());

        Span<byte> base64 = stackalloc byte[Base64Encoder.MaxCharsToFlush];
        Equal(4, encoder.Flush(base64));
    }

    [Fact]
    public static void FlushToChars()
    {
        var encoder = new Base64Encoder();
        var writer = new ArrayBufferWriter<char>();
        byte[] expected = [1, 2];

        encoder.EncodeToUtf16(expected, writer, flush: false);
        True(encoder.HasBufferedData);

        Span<char> base64 = stackalloc char[4];
        Equal(4, encoder.Flush(base64));
    }

    [Theory]
    [InlineData(17)]
    [InlineData(32)]
    [InlineData(63)]
    [InlineData(512)]
    [InlineData(1024)]
    public static void EncodeToBufferWriterSlimUtf16(int size)
    {
        var expected = RandomBytes(size);

        var encoder = new Base64Encoder();
        var writer = new BufferWriterSlim<char>();

        encoder.EncodeToUtf16(expected.AsSpan(0, size / 2), ref writer, flush: false);
        encoder.EncodeToUtf16(expected.AsSpan().Slice(size / 2), ref writer, flush: true);

        var decoder = new Base64Decoder();
        using var actual = decoder.DecodeFromUtf16(writer.ToString());

        Equal(expected, actual.Span.ToArray());
    }

    [Theory]
    [InlineData(17)]
    [InlineData(32)]
    [InlineData(63)]
    [InlineData(512)]
    [InlineData(1024)]
    public static void EncodeToBufferWriterSlimUtf8(int size)
    {
        var expected = RandomBytes(size);

        var encoder = new Base64Encoder();
        var writer = new BufferWriterSlim<byte>();

        encoder.EncodeToUtf8(expected.AsSpan(0, size / 2), ref writer, flush: false);
        encoder.EncodeToUtf8(expected.AsSpan().Slice(size / 2), ref writer, flush: true);

        var decoder = new Base64Decoder();
        using var actual = decoder.DecodeFromUtf8(writer.WrittenSpan);

        Equal(expected, actual.Span.ToArray());
    }

    [Theory]
    [InlineData(17)]
    [InlineData(32)]
    [InlineData(63)]
    [InlineData(512)]
    [InlineData(1024)]
    public static async Task EncodeToCharsAsync(int size)
    {
        var expected = RandomBytes(size);
        string base64;

        // encode
        using (var source = new MemoryStream(expected))
        {
            using var destination = new StringWriter();

            await foreach (var chunk in Base64Encoder.EncodeToUtf16Async(source.ReadAllAsync(16)))
            {
                await destination.WriteAsync(chunk);
            }

            await destination.FlushAsync();
            base64 = destination.ToString();
        }

        // decode
        using (var source = new StringReader(base64))
        {
            using var destination = new MemoryStream(expected.Length);

            await foreach (var chunk in Base64Decoder.DecodeFromUtf16Async(source.ReadAllAsync(16)))
            {
                await destination.WriteAsync(chunk);
            }

            await destination.FlushAsync();
            Equal(expected, destination.ToArray());
        }
    }

    [Theory]
    [InlineData(17)]
    [InlineData(32)]
    [InlineData(63)]
    [InlineData(512)]
    [InlineData(1024)]
    public static async Task EncodeToUtf8Async(int size)
    {
        var expected = RandomBytes(size);
        byte[] base64;

        // encode
        using (var source = new MemoryStream(expected))
        {
            using var destination = new MemoryStream();

            await foreach (var chunk in Base64Encoder.EncodeToUtf8Async(source.ReadAllAsync(16)))
            {
                await destination.WriteAsync(chunk);
            }

            await destination.FlushAsync();
            base64 = destination.ToArray();
        }

        // decode
        using (var source = new MemoryStream(base64))
        {
            using var destination = new MemoryStream(expected.Length);

            await foreach (var chunk in Base64Decoder.DecodeFromUtf8Async(source.ReadAllAsync(16)))
            {
                await destination.WriteAsync(chunk);
            }

            await destination.FlushAsync();
            Equal(expected, destination.ToArray());
        }
    }

    [Fact]
    public static void CheckEndOfUnicodeData()
    {
        var decoder = new Base64Decoder();
        var owner = decoder.DecodeFromUtf16("AA==");
        owner.Dispose();
        False(decoder.NeedMoreData);

        Throws<FormatException>(() => decoder.DecodeFromUtf16("A=="));
    }

    [Fact]
    public static void CheckEndOfUnicodeDataWithBufferWriter()
    {
        var decoder = new Base64Decoder();
        var writer = new ArrayBufferWriter<byte>();
        decoder.DecodeFromUtf16("AA==", writer);
        False(decoder.NeedMoreData);
        Equal(1, writer.WrittenCount);

        Throws<FormatException>(() => decoder.DecodeFromUtf16("A==", writer));
    }

    [Fact]
    public static void CheckEndOfUtf8Data()
    {
        var decoder = new Base64Decoder();
        var owner = decoder.DecodeFromUtf8("AA=="u8);
        owner.Dispose();
        False(decoder.NeedMoreData);

        Throws<FormatException>(() => decoder.DecodeFromUtf8("A=="u8));
    }

    [Fact]
    public static void CheckEndOfUtf8DataWithBufferWriter()
    {
        var decoder = new Base64Decoder();
        var writer = new ArrayBufferWriter<byte>();
        decoder.DecodeFromUtf8("AA=="u8, writer);
        False(decoder.NeedMoreData);
        Equal(1, writer.WrittenCount);

        Throws<FormatException>(() => decoder.DecodeFromUtf8("A=="u8, writer));
    }
}
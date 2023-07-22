using System.Buffers;
using System.Text;

namespace DotNext.Text;

using Buffers;
using IO;

[Obsolete]
public sealed class Base64Tests : Test
{
    [Theory]
    [InlineData(17, 256)]
    [InlineData(12, 256)]
    [InlineData(32, 256)]
    [InlineData(512, 1024)]
    public static void DecodeBase64BytesToBufferWriter(int chunkSize, int size)
    {
        var expected = RandomBytes(size);
        var base64 = ToReadOnlySequence<byte>(Encoding.UTF8.GetBytes(Convert.ToBase64String(expected)), chunkSize);
        var actual = new ArrayBufferWriter<byte>();
        var decoder = new Base64Decoder();
        decoder.Decode(base64, actual);
        False(decoder.NeedMoreData);

        Equal(expected, actual.WrittenSpan.ToArray());
    }

    [Theory]
    [InlineData(17)]
    [InlineData(32)]
    [InlineData(63)]
    public static void DecodeBase64BytesToMemoryBlock(int size)
    {
        var expected = RandomBytes(size);
        ReadOnlySpan<byte> base64 = Encoding.UTF8.GetBytes(Convert.ToBase64String(expected));
        var decoder = new Base64Decoder();
        using var actual = decoder.Decode(base64);
        False(decoder.NeedMoreData);

        Equal(expected, actual.Span.ToArray());
    }

    [Theory]
    [InlineData(17, 256)]
    [InlineData(12, 256)]
    [InlineData(32, 256)]
    [InlineData(512, 1024)]
    public static void DecodeBase64BytesToCallback(int chunkSize, int size)
    {
        var expected = RandomBytes(size);
        var base64 = ToReadOnlySequence<byte>(Encoding.UTF8.GetBytes(Convert.ToBase64String(expected)), chunkSize);
        var actual = new ArrayBufferWriter<byte>();
        var decoder = new Base64Decoder();
        foreach (var segment in base64)
            decoder.Decode(segment.Span, static (input, output) => output.Write(input), actual);
        False(decoder.NeedMoreData);

        Equal(expected, actual.WrittenSpan.ToArray());
    }

    [Theory]
    [InlineData(17, 256)]
    [InlineData(12, 256)]
    [InlineData(32, 256)]
    [InlineData(512, 1024)]
    public static unsafe void DecodeBase64BytesToFunctionPointer(int chunkSize, int size)
    {
        var expected = RandomBytes(size);
        var base64 = ToReadOnlySequence<byte>(Encoding.UTF8.GetBytes(Convert.ToBase64String(expected)), chunkSize);
        var actual = new ArrayBufferWriter<byte>();
        var decoder = new Base64Decoder();
        foreach (var segment in base64)
            decoder.Decode(segment.Span, &Callback, actual);
        False(decoder.NeedMoreData);

        Equal(expected, actual.WrittenSpan.ToArray());

        static void Callback(ReadOnlySpan<byte> input, ArrayBufferWriter<byte> output)
            => output.Write(input);
    }

    [Fact]
    public static void DecodeInvalidBlock()
    {
        var base64 = Encoding.UTF8.GetBytes("AB");
        var decoder = new Base64Decoder();
        using var writer = new PooledArrayBufferWriter<byte>();
        decoder.Decode(base64, writer);
        True(decoder.NeedMoreData);
        Equal(0, writer.WrittenCount);
    }

    [Theory]
    [InlineData(17, 256)]
    [InlineData(12, 256)]
    [InlineData(32, 256)]
    [InlineData(512, 1024)]
    public static void DecodeBase64CharsToBufferWriter(int chunkSize, int size)
    {
        var expected = RandomBytes(size);
        var base64 = ToReadOnlySequence<char>(Convert.ToBase64String(expected).AsMemory(), chunkSize);
        var actual = new ArrayBufferWriter<byte>();
        var decoder = new Base64Decoder();
        decoder.Decode(base64, actual);
        False(decoder.NeedMoreData);

        Equal(expected, actual.WrittenSpan.ToArray());
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
        using var actual = decoder.Decode(base64);
        False(decoder.NeedMoreData);

        Equal(expected, actual.Span.ToArray());
    }

    [Theory]
    [InlineData(25, 25)]
    [InlineData(17, 256)]
    [InlineData(12, 256)]
    [InlineData(32, 256)]
    [InlineData(512, 1024)]
    public static void DecodeBase64CharsToCallback(int chunkSize, int size)
    {
        var expected = RandomBytes(size);
        var base64 = ToReadOnlySequence<char>(Convert.ToBase64String(expected).AsMemory(), chunkSize);
        var actual = new ArrayBufferWriter<byte>();
        var decoder = new Base64Decoder();
        foreach (var segment in base64)
            decoder.Decode(segment.Span, static (input, output) => output.Write(input), actual);
        False(decoder.NeedMoreData);

        Equal(expected, actual.WrittenSpan.ToArray());
    }

    [Theory]
    [InlineData(25, 25)]
    [InlineData(17, 256)]
    [InlineData(12, 256)]
    [InlineData(32, 256)]
    [InlineData(512, 1024)]
    public static unsafe void DecodeBase64CharsToFunctionPointer(int chunkSize, int size)
    {
        var expected = RandomBytes(size);
        var base64 = ToReadOnlySequence<char>(Convert.ToBase64String(expected).AsMemory(), chunkSize);
        var actual = new ArrayBufferWriter<byte>();
        var decoder = new Base64Decoder();
        foreach (var segment in base64)
            decoder.Decode(segment.Span, &Callback, actual);
        False(decoder.NeedMoreData);

        Equal(expected, actual.WrittenSpan.ToArray());

        static void Callback(ReadOnlySpan<byte> input, ArrayBufferWriter<byte> output)
            => output.Write(input);
    }

    [Theory]
    [InlineData(25, 25)]
    [InlineData(17, 256)]
    [InlineData(12, 256)]
    [InlineData(32, 256)]
    [InlineData(512, 1024)]
    public static void DecodeBase64BytesToStream(int chunkSize, int size)
    {
        var expected = RandomBytes(size);
        var base64 = ToReadOnlySequence<byte>(Encoding.UTF8.GetBytes(Convert.ToBase64String(expected)), chunkSize);
        var actual = new ArrayBufferWriter<byte>();
        var decoder = new Base64Decoder();

        using var ms = new MemoryStream(size);
        foreach (var segment in base64)
            decoder.Decode(segment.Span, ms);

        ms.Flush();
        Equal(expected, ms.ToArray());
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
        Equal(0, encoder.BufferedDataSize);

        var decoder = new Base64Decoder();
        using var actual = decoder.Decode(writer.WrittenSpan);

        Equal(expected, actual.Span.ToArray());
    }

    [Theory]
    [InlineData(17)]
    [InlineData(32)]
    [InlineData(63)]
    [InlineData(512)]
    [InlineData(1024)]
    public static void EncodeToBufferWriterChars(int size)
    {
        var expected = RandomBytes(size);

        var encoder = new Base64Encoder();
        var writer = new ArrayBufferWriter<char>();

        encoder.EncodeToChars(expected.AsSpan(0, size / 2), writer, flush: false);
        encoder.EncodeToChars(expected.AsSpan().Slice(size / 2), writer, flush: true);

        var decoder = new Base64Decoder();
        using var actual = decoder.Decode(writer.WrittenSpan);

        Equal(expected, actual.Span.ToArray());
    }

    [Theory]
    [InlineData(17)]
    [InlineData(32)]
    [InlineData(63)]
    [InlineData(512)]
    [InlineData(1024)]
    public static void EncodeToBytes(int size)
    {
        var expected = RandomBytes(size);

        var encoder = new Base64Encoder();
        using var base64 = encoder.EncodeToUtf8(expected, flush: true);

        var decoder = new Base64Decoder();
        using var actual = decoder.Decode(base64.Span);

        Equal(expected, actual.Span.ToArray());
    }

    [Theory]
    [InlineData(17)]
    [InlineData(32)]
    [InlineData(63)]
    [InlineData(512)]
    [InlineData(1024)]
    public static void EncodeToChars(int size)
    {
        var expected = RandomBytes(size);

        var encoder = new Base64Encoder();
        using var base64 = encoder.EncodeToChars(expected, flush: true);

        var decoder = new Base64Decoder();
        using var actual = decoder.Decode(base64.Span);

        Equal(expected, actual.Span.ToArray());
    }

    [Fact]
    public static void FlushToBytes()
    {
        var encoder = new Base64Encoder();
        var writer = new ArrayBufferWriter<byte>();
        byte[] expected = { 1, 2 };

        encoder.EncodeToUtf8(expected, writer, flush: false);
        True(encoder.HasBufferedData);

        Span<byte> base64 = stackalloc byte[4];
        Equal(2, encoder.GetBufferedData(base64));
        Equal(expected, base64.Slice(0, 2).ToArray());

        Equal(4, encoder.Flush(base64));
    }

    [Fact]
    public static void FlushToChars()
    {
        var encoder = new Base64Encoder();
        var writer = new ArrayBufferWriter<char>();
        byte[] expected = { 1, 2 };

        encoder.EncodeToChars(expected, writer, flush: false);
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
    public static void EncodeToStringBuilder(int size)
    {
        var expected = RandomBytes(size);

        var encoder = new Base64Encoder();
        var writer = new StringBuilder();

        encoder.EncodeToChars(expected.AsSpan(0, size / 2), writer, flush: false);
        encoder.EncodeToChars(expected.AsSpan().Slice(size / 2), writer, flush: true);

        var decoder = new Base64Decoder();
        using var actual = decoder.Decode(writer.ToString());

        Equal(expected, actual.Span.ToArray());
    }

    [Theory]
    [InlineData(17)]
    [InlineData(32)]
    [InlineData(63)]
    [InlineData(512)]
    [InlineData(1024)]
    public static void EncodeToTextWriter(int size)
    {
        var expected = RandomBytes(size);

        var encoder = new Base64Encoder();
        using var writer = new StringWriter();

        encoder.EncodeToChars(expected.AsSpan(0, size / 2), writer, flush: false);
        encoder.EncodeToChars(expected.AsSpan().Slice(size / 2), writer, flush: true);

        var decoder = new Base64Decoder();
        using var actual = decoder.Decode(writer.ToString());

        Equal(expected, actual.Span.ToArray());
    }

    [Theory]
    [InlineData(17)]
    [InlineData(32)]
    [InlineData(63)]
    [InlineData(512)]
    [InlineData(1024)]
    public static void EncodeToCallbackUtf8(int size)
    {
        var expected = RandomBytes(size);

        var encoder = new Base64Encoder();
        False(encoder.HasBufferedData);
        var writer = new ArrayBufferWriter<byte>();

        encoder.EncodeToUtf8(expected.AsSpan(0, size / 2), Write, writer, flush: false);
        encoder.EncodeToUtf8(expected.AsSpan().Slice(size / 2), Write, writer, flush: true);
        Equal(0, encoder.BufferedDataSize);

        var decoder = new Base64Decoder();
        using var actual = decoder.Decode(writer.WrittenSpan);

        Equal(expected, actual.Span.ToArray());

        static void Write(ReadOnlySpan<byte> input, ArrayBufferWriter<byte> output)
            => output.Write(input);
    }

    [Theory]
    [InlineData(17)]
    [InlineData(32)]
    [InlineData(63)]
    [InlineData(512)]
    [InlineData(1024)]
    public static void EncodeToCallbackChars(int size)
    {
        var expected = RandomBytes(size);

        var encoder = new Base64Encoder();
        False(encoder.HasBufferedData);
        var writer = new ArrayBufferWriter<char>();

        encoder.EncodeToChars(expected.AsSpan(0, size / 2), Write, writer, flush: false);
        encoder.EncodeToChars(expected.AsSpan().Slice(size / 2), Write, writer, flush: true);
        Equal(0, encoder.BufferedDataSize);

        var decoder = new Base64Decoder();
        using var actual = decoder.Decode(writer.WrittenSpan);

        Equal(expected, actual.Span.ToArray());

        static void Write(ReadOnlySpan<char> input, ArrayBufferWriter<char> output)
            => output.Write(input);
    }

    [Theory]
    [InlineData(17)]
    [InlineData(32)]
    [InlineData(63)]
    [InlineData(512)]
    [InlineData(1024)]
    public static unsafe void EncodeToFunctionPointerUtf8(int size)
    {
        var expected = RandomBytes(size);

        var encoder = new Base64Encoder();
        False(encoder.HasBufferedData);
        var writer = new ArrayBufferWriter<byte>();

        encoder.EncodeToUtf8(expected.AsSpan(0, size / 2), &Write, writer, flush: false);
        encoder.EncodeToUtf8(expected.AsSpan().Slice(size / 2), &Write, writer, flush: true);
        Equal(0, encoder.BufferedDataSize);

        var decoder = new Base64Decoder();
        using var actual = decoder.Decode(writer.WrittenSpan);

        Equal(expected, actual.Span.ToArray());

        static void Write(ReadOnlySpan<byte> input, ArrayBufferWriter<byte> output)
            => output.Write(input);
    }

    [Theory]
    [InlineData(17)]
    [InlineData(32)]
    [InlineData(63)]
    [InlineData(512)]
    [InlineData(1024)]
    public static unsafe void EncodeToFunctionPointerChars(int size)
    {
        var expected = RandomBytes(size);

        var encoder = new Base64Encoder();
        False(encoder.HasBufferedData);
        var writer = new ArrayBufferWriter<char>();

        encoder.EncodeToChars(expected.AsSpan(0, size / 2), &Write, writer, flush: false);
        encoder.EncodeToChars(expected.AsSpan().Slice(size / 2), &Write, writer, flush: true);
        Equal(0, encoder.BufferedDataSize);

        var decoder = new Base64Decoder();
        using var actual = decoder.Decode(writer.WrittenSpan);

        Equal(expected, actual.Span.ToArray());

        static void Write(ReadOnlySpan<char> input, ArrayBufferWriter<char> output)
            => output.Write(input);
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

            await foreach (var chunk in Base64Encoder.EncodeToCharsAsync(source.ReadAllAsync(16)))
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

            await foreach (var chunk in Base64Decoder.DecodeAsync(source.ReadAllAsync(16)))
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

            await foreach (var chunk in Base64Decoder.DecodeAsync(source.ReadAllAsync(16)))
            {
                await destination.WriteAsync(chunk);
            }

            await destination.FlushAsync();
            Equal(expected, destination.ToArray());
        }
    }
}
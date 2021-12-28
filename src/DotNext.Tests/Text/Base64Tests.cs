using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace DotNext.Text
{
    using Buffers;

    [ExcludeFromCodeCoverage]
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
        public static void DecodeBase64CharsToByteBuffer(int chunkSize, int size)
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
        [InlineData(25)]
        [InlineData(256)]
        public static void DecodeBase64FromUtf8(int size)
        {
            var expected = RandomBytes(size);
            var utf8Chars = Encoding.UTF8.GetBytes(Convert.ToBase64String(expected));

            using var actual = Base64Decoder.Decode(utf8Chars);
            Equal(expected, actual.Span.ToArray());
        }

        [Fact]
        public static void DecodeBase64FromInvalidUtf8()
        {
            var utf8Chars = Encoding.UTF8.GetBytes("==");

            using var actual = Base64Decoder.Decode(utf8Chars);
            True(actual.IsEmpty);
        }

        [Theory]
        [InlineData(25)]
        [InlineData(256)]
        public static void DecodeBase64FromUnicode(int size)
        {
            var expected = RandomBytes(size);
            ReadOnlySpan<char> utf8Chars = Convert.ToBase64String(expected);

            using var actual = Base64Decoder.Decode(utf8Chars);
            Equal(expected, actual.Span.ToArray());
        }

        [Fact]
        public static void DecodeBase64FromInvalidUnicode()
        {
            True(Base64Decoder.Decode(ReadOnlySpan<char>.Empty).IsEmpty);
            Throws<FormatException>(static () => Base64Decoder.Decode("=="));
        }
    }
}
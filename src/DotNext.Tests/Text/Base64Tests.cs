using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Xunit;

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
            decoder.Decode(base64, new ValueReadOnlySpanAction<byte, IBufferWriter<byte>>((input, output) => output.Write(input)), actual);
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
            decoder.Decode(base64, new ValueReadOnlySpanAction<byte, IBufferWriter<byte>>((input, output) => output.Write(input)), actual);
            False(decoder.NeedMoreData);

            Equal(expected, actual.WrittenSpan.ToArray());
        }
    }
}
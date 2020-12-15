using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Xunit;

namespace DotNext.Text
{
    [ExcludeFromCodeCoverage]
    public sealed class Base64Tests : Test
    {
        [Theory]
        [InlineData(17)]
        [InlineData(12)]
        [InlineData(32)]
        public static void DecodeBase64ToBufferWriter(int chunkSize)
        {
            var expected = RandomBytes(256);
            var base64 = ToReadOnlySequence<byte>(Encoding.UTF8.GetBytes(Convert.ToBase64String(expected)), chunkSize);
            var actual = new ArrayBufferWriter<byte>();
            var decoder = new Base64Decoder();
            foreach (var block in base64)
                decoder.Decode(block.Span, actual);

            Equal(expected, actual.WrittenSpan.ToArray());
        }

        [Theory]
        [InlineData(17)]
        [InlineData(12)]
        [InlineData(32)]
        public static void DecodeBase64ToCallback(int chunkSize)
        {
            var expected = RandomBytes(256);
            var base64 = ToReadOnlySequence<byte>(Encoding.UTF8.GetBytes(Convert.ToBase64String(expected)), chunkSize);
            var actual = new ArrayBufferWriter<byte>();
            var decoder = new Base64Decoder();
            foreach (var block in base64)
                decoder.Decode(block.Span, new ValueReadOnlySpanAction<byte, IBufferWriter<byte>>((input, output) => output.Write(input)), actual);

            Equal(expected, actual.WrittenSpan.ToArray());
        }
    }
}
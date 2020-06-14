using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Buffers
{
    using IAsyncBinaryReader = IO.IAsyncBinaryReader;
    using StringLengthEncoding = IO.StringLengthEncoding;

    [ExcludeFromCodeCoverage]
    public sealed class BufferWriterTests : Test
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public static async Task ReadBlittableTypes(bool littleEndian)
        {
            var writer = new ArrayBufferWriter<byte>();
            writer.Write(10M);
            writer.WriteInt64(42L, littleEndian);
            writer.WriteInt32(44, littleEndian);
            writer.WriteInt16(46, littleEndian);
            IAsyncBinaryReader reader = IAsyncBinaryReader.Create(writer.WrittenMemory);
            Equal(10M, await reader.ReadAsync<decimal>());
            Equal(42L, await reader.ReadInt64Async(littleEndian));
            Equal(44, await reader.ReadInt32Async(littleEndian));
            Equal(46, await reader.ReadInt16Async(littleEndian));
        }

        private static async Task ReadWriteStringUsingEncodingAsync(string value, Encoding encoding, int bufferSize, StringLengthEncoding? lengthEnc)
        {
            var writer = new ArrayBufferWriter<byte>();
            writer.WriteString(value.AsSpan(), encoding, bufferSize, lengthEnc);
            IAsyncBinaryReader reader = IAsyncBinaryReader.Create(writer.WrittenMemory);
            var result = await (lengthEnc is null ?
                reader.ReadStringAsync(encoding.GetByteCount(value), encoding) :
                reader.ReadStringAsync(lengthEnc.GetValueOrDefault(), encoding));
            Equal(value, result);
        }

        [Theory]
        [InlineData(0, null)]
        [InlineData(0, StringLengthEncoding.Compressed)]
        [InlineData(0, StringLengthEncoding.Plain)]
        [InlineData(0, StringLengthEncoding.PlainLittleEndian)]
        [InlineData(0, StringLengthEncoding.PlainBigEndian)]
        [InlineData(128, null)]
        [InlineData(128, StringLengthEncoding.Compressed)]
        [InlineData(128, StringLengthEncoding.Plain)]
        [InlineData(128, StringLengthEncoding.PlainLittleEndian)]
        [InlineData(128, StringLengthEncoding.PlainBigEndian)]
        public static async Task ReadWriteBufferedStringAsync(int bufferSize, StringLengthEncoding? lengthEnc)
        {
            const string testString1 = "Hello, world!&*(@&*(fghjwgfwffgw";
            await ReadWriteStringUsingEncodingAsync(testString1, Encoding.UTF8, bufferSize, lengthEnc);
            await ReadWriteStringUsingEncodingAsync(testString1, Encoding.Unicode, bufferSize, lengthEnc);
            await ReadWriteStringUsingEncodingAsync(testString1, Encoding.UTF7, bufferSize, lengthEnc);
            await ReadWriteStringUsingEncodingAsync(testString1, Encoding.UTF32, bufferSize, lengthEnc);
            await ReadWriteStringUsingEncodingAsync(testString1, Encoding.ASCII, bufferSize, lengthEnc);
            const string testString2 = "������, ���!";
            await ReadWriteStringUsingEncodingAsync(testString2, Encoding.UTF8, bufferSize, lengthEnc);
            await ReadWriteStringUsingEncodingAsync(testString2, Encoding.Unicode, bufferSize, lengthEnc);
            await ReadWriteStringUsingEncodingAsync(testString2, Encoding.UTF7, bufferSize, lengthEnc);
            await ReadWriteStringUsingEncodingAsync(testString2, Encoding.UTF32, bufferSize, lengthEnc);
        }
    }
}
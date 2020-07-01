using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using static System.Globalization.CultureInfo;

namespace DotNext.Buffers
{
    using Text;
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

        [Fact]
        public static void ArrayBufferToString()
        {
            var writer = new ArrayBufferWriter<char>();
            writer.Write("Hello, world");
            writer.Write('!');
            Equal("Hello, world!", writer.BuildString());
        }

        public static IEnumerable<object[]> CharWriters()
        {
            yield return new object[] { new PooledBufferWriter<char>(MemoryPool<char>.Shared.ToAllocator()) };
            yield return new object[] { new PooledArrayBufferWriter<char>() };
        }

        [Theory]
        [MemberData(nameof(CharWriters))]
        public static void MutableStringBuffer(MemoryWriter<char> writer)
        {
            using (writer)
            {
                writer.Write("Hello, world");
                writer.Write('!');
                writer.WriteLine();
                writer.WriteInt32(42, provider: InvariantCulture);
                writer.WriteUInt32(56U, provider: InvariantCulture);
                writer.WriteByte(10, provider: InvariantCulture);
                writer.WriteSByte(22, provider: InvariantCulture);
                writer.WriteInt16(88, provider: InvariantCulture);
                writer.WriteUInt16(99, provider: InvariantCulture);
                writer.WriteInt64(77, provider: InvariantCulture);
                writer.WriteUInt64(66, provider: InvariantCulture);

                var guid = Guid.NewGuid();
                writer.WriteGuid(guid);

                var dt = DateTime.Now;
                writer.WriteDateTime(dt, provider: InvariantCulture);
                
                var dto = DateTimeOffset.Now;
                writer.WriteDateTime(dto, provider: InvariantCulture);

                writer.WriteBoolean(true);
                writer.WriteDecimal(42.5M, provider: InvariantCulture);
                writer.WriteSingle(32.2F, provider: InvariantCulture);
                writer.WriteDouble(56.6D, provider: InvariantCulture);

                Equal("Hello, world!" + Environment.NewLine + "4256102288997766" + guid + dt.ToString(InvariantCulture) + dto.ToString(InvariantCulture) + bool.TrueString + "42.532.256.6", writer.BuildString());
            }
        }

        public static IEnumerable<object[]> ByteWriters()
        {
            yield return new object[] { new PooledBufferWriter<byte>(MemoryPool<byte>.Shared.ToAllocator()), Encoding.UTF32 };
            yield return new object[] { new PooledArrayBufferWriter<byte>(), Encoding.UTF8 };
        }

        [Theory]
        [MemberData(nameof(ByteWriters))]
        public static void EncodeAsString(MemoryWriter<byte> writer, Encoding encoding)
        {
            var encodingContext = new EncodingContext(encoding, true);
            using (writer)
            {
                var g = Guid.NewGuid();
                var dt = DateTime.Now;
                var dto = DateTimeOffset.Now;
                writer.WriteInt64(42L, in encodingContext, StringLengthEncoding.Plain, provider: InvariantCulture);
                writer.WriteUInt64(12UL, in encodingContext, StringLengthEncoding.PlainLittleEndian, provider: InvariantCulture);
                writer.WriteInt32(34, in encodingContext, StringLengthEncoding.PlainBigEndian, provider: InvariantCulture);
                writer.WriteUInt32(78, in encodingContext, StringLengthEncoding.Plain, provider: InvariantCulture);
                writer.WriteInt16(90, in encodingContext, StringLengthEncoding.Plain, provider: InvariantCulture);
                writer.WriteUInt16(12, in encodingContext, StringLengthEncoding.Plain, format: "X", provider: InvariantCulture);
                writer.WriteByte(10, in encodingContext, StringLengthEncoding.Plain, format: "X", provider: InvariantCulture);
                writer.WriteSByte(11, in encodingContext, StringLengthEncoding.Plain, format: "X", provider: InvariantCulture);
                writer.WriteBoolean(true, in encodingContext, StringLengthEncoding.Plain);
                writer.WriteGuid(g, in encodingContext, StringLengthEncoding.Plain);
                writer.WriteDateTime(dt, in encodingContext, StringLengthEncoding.Plain, provider: InvariantCulture);
                writer.WriteDateTime(dto, in encodingContext, StringLengthEncoding.Plain, provider: InvariantCulture);
                writer.WriteDecimal(42.5M, in encodingContext, StringLengthEncoding.Plain, provider: InvariantCulture);
                writer.WriteSingle(32.2F, in encodingContext, StringLengthEncoding.Plain, provider: InvariantCulture);
                writer.WriteDouble(56.6, in encodingContext, StringLengthEncoding.Plain, provider: InvariantCulture);

                var decodingContext = new DecodingContext(encoding, true);
                var reader = IAsyncBinaryReader.Create(writer.WrittenMemory);
                Equal("42", reader.ReadString(StringLengthEncoding.Plain, in decodingContext));
                Equal("12", reader.ReadString(StringLengthEncoding.PlainLittleEndian, in decodingContext));
                Equal("34", reader.ReadString(StringLengthEncoding.PlainBigEndian, in decodingContext));
                Equal("78", reader.ReadString(StringLengthEncoding.Plain, in decodingContext));
                Equal("90", reader.ReadString(StringLengthEncoding.Plain, in decodingContext));
                Equal("C", reader.ReadString(StringLengthEncoding.Plain, in decodingContext));
                Equal("A", reader.ReadString(StringLengthEncoding.Plain, in decodingContext));
                Equal("B", reader.ReadString(StringLengthEncoding.Plain, in decodingContext));
                Equal(bool.TrueString, reader.ReadString(StringLengthEncoding.Plain, in decodingContext));
                Equal(g.ToString(), reader.ReadString(StringLengthEncoding.Plain, in decodingContext));
                Equal(dt.ToString(InvariantCulture), reader.ReadString(StringLengthEncoding.Plain, in decodingContext));
                Equal(dto.ToString(InvariantCulture), reader.ReadString(StringLengthEncoding.Plain, in decodingContext));
                Equal("42.5", reader.ReadString(StringLengthEncoding.Plain, in decodingContext));
                Equal("32.2", reader.ReadString(StringLengthEncoding.Plain, in decodingContext));
                Equal("56.6", reader.ReadString(StringLengthEncoding.Plain, in decodingContext));
            }
        }
    }
}
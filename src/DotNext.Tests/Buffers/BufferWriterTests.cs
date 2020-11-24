using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
            yield return new object[] { new SparseBufferWriter<char>() };
            yield return new object[] { new SparseBufferWriter<char>(32) };
        }

        [Theory]
        [MemberData(nameof(CharWriters))]
        public static void MutableStringBuffer<TWriter>(TWriter writer)
            where TWriter : class, IBufferWriter<char>, IGrowableBuffer<char>
        {
            using (writer)
            {
                writer.Write("Hello, world");
                writer.Write('!');
                writer.WriteLine("!!");
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
                writer.WriteDateTimeOffset(dto, provider: InvariantCulture);

                writer.WriteDecimal(42.5M, provider: InvariantCulture);
                writer.WriteSingle(32.2F, provider: InvariantCulture);
                writer.WriteDouble(56.6D, provider: InvariantCulture);

                Equal("Hello, world!!!" + Environment.NewLine + "4256102288997766" + guid + dt.ToString(InvariantCulture) + dto.ToString(InvariantCulture) + "42.532.256.6", writer.BuildString());
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
                writer.WriteInt64(42L, StringLengthEncoding.Plain, in encodingContext, provider: InvariantCulture);
                writer.WriteUInt64(12UL, StringLengthEncoding.PlainLittleEndian, in encodingContext, provider: InvariantCulture);
                writer.WriteInt32(34, StringLengthEncoding.PlainBigEndian, in encodingContext, provider: InvariantCulture);
                writer.WriteUInt32(78, StringLengthEncoding.Plain, in encodingContext, provider: InvariantCulture);
                writer.WriteInt16(90, StringLengthEncoding.Plain, in encodingContext, provider: InvariantCulture);
                writer.WriteUInt16(12, StringLengthEncoding.Plain, in encodingContext, format: "X", provider: InvariantCulture);
                writer.WriteUInt16(12, StringLengthEncoding.Plain, in encodingContext, provider: InvariantCulture);
                writer.WriteByte(10, StringLengthEncoding.Plain, in encodingContext, format: "X", provider: InvariantCulture);
                writer.WriteSByte(11, StringLengthEncoding.Plain, in encodingContext, format: "X", provider: InvariantCulture);
                writer.WriteByte(10, StringLengthEncoding.Plain, in encodingContext, provider: InvariantCulture);
                writer.WriteSByte(11, StringLengthEncoding.Plain, in encodingContext, provider: InvariantCulture);
                writer.WriteGuid(g, StringLengthEncoding.Plain, in encodingContext);
                writer.WriteGuid(g, StringLengthEncoding.Plain, in encodingContext, format: "X");
                writer.WriteDateTime(dt, StringLengthEncoding.Plain, in encodingContext, format: "O", provider: InvariantCulture);
                writer.WriteDateTimeOffset(dto, StringLengthEncoding.Plain, in encodingContext, format: "O", provider: InvariantCulture);
                writer.WriteDateTime(dt, StringLengthEncoding.Plain, in encodingContext, format: "O", provider: InvariantCulture);
                writer.WriteDateTimeOffset(dto, StringLengthEncoding.Plain, in encodingContext, format: "O", provider: InvariantCulture);
                writer.WriteDecimal(42.5M, StringLengthEncoding.Plain, in encodingContext, provider: InvariantCulture);
                writer.WriteSingle(32.2F, StringLengthEncoding.Plain, in encodingContext, provider: InvariantCulture);
                writer.WriteDouble(56.6D, StringLengthEncoding.Plain, in encodingContext, provider: InvariantCulture);

                var decodingContext = new DecodingContext(encoding, true);
                var reader = IAsyncBinaryReader.Create(writer.WrittenMemory);
                Equal(42L, reader.ReadInt64(StringLengthEncoding.Plain, in decodingContext, provider: InvariantCulture));
                Equal(12UL, reader.ReadUInt64(StringLengthEncoding.PlainLittleEndian, in decodingContext, provider: InvariantCulture));
                Equal(34, reader.ReadInt32(StringLengthEncoding.PlainBigEndian, in decodingContext, provider: InvariantCulture));
                Equal(78U, reader.ReadUInt32(StringLengthEncoding.Plain, in decodingContext, provider: InvariantCulture));
                Equal(90, reader.ReadInt16(StringLengthEncoding.Plain, in decodingContext, provider: InvariantCulture));
                Equal("C", reader.ReadString(StringLengthEncoding.Plain, in decodingContext));
                Equal(12, reader.ReadUInt16(StringLengthEncoding.Plain, in decodingContext, provider: InvariantCulture));
                Equal("A", reader.ReadString(StringLengthEncoding.Plain, in decodingContext));
                Equal("B", reader.ReadString(StringLengthEncoding.Plain, in decodingContext));
                Equal(10, reader.ReadByte(StringLengthEncoding.Plain, in decodingContext, provider: InvariantCulture));
                Equal(11, reader.ReadSByte(StringLengthEncoding.Plain, in decodingContext, provider: InvariantCulture));
                Equal(g, reader.ReadGuid(StringLengthEncoding.Plain, in decodingContext));
                Equal(g, reader.ReadGuid(StringLengthEncoding.Plain, in decodingContext, "X"));
                Equal(dt, reader.ReadDateTime(StringLengthEncoding.Plain, in decodingContext, style: DateTimeStyles.RoundtripKind, provider: InvariantCulture));
                Equal(dto, reader.ReadDateTimeOffset(StringLengthEncoding.Plain, in decodingContext, style: DateTimeStyles.RoundtripKind, provider: InvariantCulture));
                Equal(dt, reader.ReadDateTime(StringLengthEncoding.Plain, in decodingContext, formats: new[] { "O" }, style: DateTimeStyles.RoundtripKind, provider: InvariantCulture));
                Equal(dto, reader.ReadDateTimeOffset(StringLengthEncoding.Plain, in decodingContext, formats: new[] { "O" }, style: DateTimeStyles.RoundtripKind, provider: InvariantCulture));
                Equal(42.5M, reader.ReadDecimal(StringLengthEncoding.Plain, in decodingContext, provider: InvariantCulture));
                Equal(32.2F, reader.ReadSingle(StringLengthEncoding.Plain, in decodingContext, provider: InvariantCulture));
                Equal(56.6D, reader.ReadDouble(StringLengthEncoding.Plain, in decodingContext, provider: InvariantCulture));
            }
        }
    }
}
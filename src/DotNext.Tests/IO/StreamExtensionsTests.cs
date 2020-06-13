using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.IO
{
    using Buffers;

    [ExcludeFromCodeCoverage]
    public sealed class StreamExtensionsTests : Test
    {
        private static void ReadStringUsingEncoding(string value, Encoding encoding, int bufferSize)
        {
            using var ms = new MemoryStream();
            ms.Write(encoding.GetBytes(value));
            ms.Position = 0;
            var buffer = new byte[bufferSize];
            Equal(value, ms.ReadString(encoding.GetByteCount(value), encoding, buffer));
        }

        private static void ReadStringUsingEncoding(string value, Encoding encoding)
        {
            using var ms = new MemoryStream();
            ms.Write(encoding.GetBytes(value));
            ms.Position = 0;
            Equal(value, ms.ReadString(encoding.GetByteCount(value), encoding));
        }

        [Fact]
        public static void ReadString()
        {
            const string testString1 = "Hello, world! &$@&@()&$YHWORww!";
            ReadStringUsingEncoding(testString1, Encoding.UTF8);
            ReadStringUsingEncoding(testString1, Encoding.Unicode);
            ReadStringUsingEncoding(testString1, Encoding.UTF7);
            ReadStringUsingEncoding(testString1, Encoding.UTF32);
            ReadStringUsingEncoding(testString1, Encoding.ASCII);
            const string testString2 = "������, ���!";
            ReadStringUsingEncoding(testString2, Encoding.UTF8);
            ReadStringUsingEncoding(testString2, Encoding.Unicode);
            ReadStringUsingEncoding(testString2, Encoding.UTF7);
            ReadStringUsingEncoding(testString2, Encoding.UTF32);
        }

        [Theory]
        [InlineData(4)]
        [InlineData(15)]
        [InlineData(128)]
        public static void ReadStringBuffered(int bufferSize)
        {
            const string testString1 = "Hello, world! &$@&@()&$YHWORww!";
            ReadStringUsingEncoding(testString1, Encoding.UTF8, bufferSize);
            ReadStringUsingEncoding(testString1, Encoding.Unicode, bufferSize);
            ReadStringUsingEncoding(testString1, Encoding.UTF7, bufferSize);
            ReadStringUsingEncoding(testString1, Encoding.UTF32, bufferSize);
            ReadStringUsingEncoding(testString1, Encoding.ASCII, bufferSize);
            const string testString2 = "������, ���!";
            ReadStringUsingEncoding(testString2, Encoding.UTF8, bufferSize);
            ReadStringUsingEncoding(testString2, Encoding.Unicode, bufferSize);
            ReadStringUsingEncoding(testString2, Encoding.UTF7, bufferSize);
            ReadStringUsingEncoding(testString2, Encoding.UTF32, bufferSize);
        }

        private static async Task ReadStringUsingEncodingAsync(string value, Encoding encoding, int bufferSize)
        {
            using var ms = new MemoryStream();
            await ms.WriteAsync(encoding.GetBytes(value));
            ms.Position = 0;
            var buffer = new byte[bufferSize];
            Equal(value, await ms.ReadStringAsync(encoding.GetByteCount(value), encoding, buffer));
        }

        private static async Task ReadStringUsingEncodingAsync(string value, Encoding encoding)
        {
            using var ms = new MemoryStream();
            await ms.WriteAsync(encoding.GetBytes(value));
            ms.Position = 0;
            Equal(value, await ms.ReadStringAsync(encoding.GetByteCount(value), encoding));
        }

        [Fact]
        public static async Task ReadStringAsync()
        {
            const string testString1 = "Hello, world! $(@$)Hjdqgd!";
            await ReadStringUsingEncodingAsync(testString1, Encoding.UTF8);
            await ReadStringUsingEncodingAsync(testString1, Encoding.Unicode);
            await ReadStringUsingEncodingAsync(testString1, Encoding.UTF7);
            await ReadStringUsingEncodingAsync(testString1, Encoding.UTF32);
            await ReadStringUsingEncodingAsync(testString1, Encoding.ASCII);
            const string testString2 = "������, ���!";
            await ReadStringUsingEncodingAsync(testString2, Encoding.UTF8);
            await ReadStringUsingEncodingAsync(testString2, Encoding.Unicode);
            await ReadStringUsingEncodingAsync(testString2, Encoding.UTF7);
            await ReadStringUsingEncodingAsync(testString2, Encoding.UTF32);
        }

        [Theory]
        [InlineData(4)]
        [InlineData(10)]
        [InlineData(15)]
        [InlineData(1024)]
        public static async Task ReadStringBufferedAsync(int bufferSize)
        {
            const string testString1 = "Hello, world! $(@$)Hjdqgd!";
            await ReadStringUsingEncodingAsync(testString1, Encoding.UTF8, bufferSize);
            await ReadStringUsingEncodingAsync(testString1, Encoding.Unicode, bufferSize);
            await ReadStringUsingEncodingAsync(testString1, Encoding.UTF7, bufferSize);
            await ReadStringUsingEncodingAsync(testString1, Encoding.UTF32, bufferSize);
            await ReadStringUsingEncodingAsync(testString1, Encoding.ASCII, bufferSize);
            const string testString2 = "������, ���!";
            await ReadStringUsingEncodingAsync(testString2, Encoding.UTF8, bufferSize);
            await ReadStringUsingEncodingAsync(testString2, Encoding.Unicode, bufferSize);
            await ReadStringUsingEncodingAsync(testString2, Encoding.UTF7, bufferSize);
            await ReadStringUsingEncodingAsync(testString2, Encoding.UTF32, bufferSize);
        }

        private static void ReadWriteStringUsingEncoding(Encoding encoding, int bufferSize, StringLengthEncoding? lengthEnc)
        {
            const string helloWorld = "Hello, world!&*(@&*(fghjwgfwffgw";
            using var ms = new MemoryStream();
            var buffer = new byte[bufferSize];
            ms.WriteString(helloWorld, encoding, buffer, lengthEnc);
            ms.Position = 0;
            var result = lengthEnc is null ?
                ms.ReadString(encoding.GetByteCount(helloWorld), encoding, buffer) :
                ms.ReadString(lengthEnc.Value, encoding, buffer);
            Equal(helloWorld, result);
        }

        private static void ReadWriteStringUsingEncoding(string value, Encoding encoding, StringLengthEncoding? lengthEnc)
        {
            using var ms = new MemoryStream();
            ms.WriteString(value, encoding, lengthEnc);
            ms.Position = 0;
            var result = lengthEnc is null ?
                ms.ReadString(encoding.GetByteCount(value), encoding) :
                ms.ReadString(lengthEnc.Value, encoding);
            Equal(value, result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(StringLengthEncoding.Compressed)]
        [InlineData(StringLengthEncoding.Plain)]
        [InlineData(StringLengthEncoding.PlainBigEndian)]
        [InlineData(StringLengthEncoding.PlainLittleEndian)]
        public static void ReadWriteString(StringLengthEncoding? lengthEnc)
        {
            const string testString1 = "Hello, world!&*(@&*(fghjwgfwffgw";
            ReadWriteStringUsingEncoding(testString1, Encoding.UTF8, lengthEnc);
            ReadWriteStringUsingEncoding(testString1, Encoding.Unicode, lengthEnc);
            ReadWriteStringUsingEncoding(testString1, Encoding.UTF7, lengthEnc);
            ReadWriteStringUsingEncoding(testString1, Encoding.UTF32, lengthEnc);
            ReadWriteStringUsingEncoding(testString1, Encoding.ASCII, lengthEnc);
            const string testString2 = "������, ���!";
            ReadWriteStringUsingEncoding(testString2, Encoding.UTF8, lengthEnc);
            ReadWriteStringUsingEncoding(testString2, Encoding.Unicode, lengthEnc);
            ReadWriteStringUsingEncoding(testString2, Encoding.UTF7, lengthEnc);
            ReadWriteStringUsingEncoding(testString2, Encoding.UTF32, lengthEnc);
        }

        [Theory]
        [InlineData(10, null)]
        [InlineData(15, null)]
        [InlineData(1024, null)]
        [InlineData(10, StringLengthEncoding.Plain)]
        [InlineData(15, StringLengthEncoding.Plain)]
        [InlineData(1024, StringLengthEncoding.Plain)]
        [InlineData(10, StringLengthEncoding.Compressed)]
        [InlineData(15, StringLengthEncoding.Compressed)]
        [InlineData(1024, StringLengthEncoding.Compressed)]
        [InlineData(10, StringLengthEncoding.PlainLittleEndian)]
        [InlineData(15, StringLengthEncoding.PlainLittleEndian)]
        [InlineData(1024, StringLengthEncoding.PlainLittleEndian)]
        [InlineData(10, StringLengthEncoding.PlainBigEndian)]
        [InlineData(15, StringLengthEncoding.PlainBigEndian)]
        [InlineData(1024, StringLengthEncoding.PlainBigEndian)]
        public static void ReadWriteBufferedString(int bufferSize, StringLengthEncoding? lengthEnc)
        {
            ReadWriteStringUsingEncoding(Encoding.UTF8, bufferSize, lengthEnc);
            ReadWriteStringUsingEncoding(Encoding.Unicode, bufferSize, lengthEnc);
            ReadWriteStringUsingEncoding(Encoding.UTF7, bufferSize, lengthEnc);
            ReadWriteStringUsingEncoding(Encoding.UTF32, bufferSize, lengthEnc);
            ReadWriteStringUsingEncoding(Encoding.ASCII, bufferSize, lengthEnc);
        }

        private static async Task ReadWriteStringUsingEncodingAsync(string value, Encoding encoding, int bufferSize, StringLengthEncoding? lengthEnc)
        {
            using var ms = new MemoryStream();
            var buffer = new byte[bufferSize];
            await ms.WriteStringAsync(value.AsMemory(), encoding, buffer, lengthEnc);
            ms.Position = 0;
            var reader = IAsyncBinaryReader.Create(ms, buffer);
            var result = await (lengthEnc is null ?
                reader.ReadStringAsync(encoding.GetByteCount(value), encoding) :
                reader.ReadStringAsync(lengthEnc.Value, encoding));
            Equal(value, result);
        }

        private static async Task ReadWriteStringUsingEncodingAsync(string value, Encoding encoding, StringLengthEncoding? lengthEnc)
        {
            using var ms = new MemoryStream();
            await ms.WriteStringAsync(value.AsMemory(), encoding, lengthEnc);
            ms.Position = 0;
            var result = await (lengthEnc is null ?
                ms.ReadStringAsync(encoding.GetByteCount(value), encoding) :
                ms.ReadStringAsync(lengthEnc.Value, encoding));
            Equal(value, result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(StringLengthEncoding.Compressed)]
        [InlineData(StringLengthEncoding.Plain)]
        [InlineData(StringLengthEncoding.PlainLittleEndian)]
        [InlineData(StringLengthEncoding.PlainBigEndian)]
        public static async Task ReadWriteStringAsync(StringLengthEncoding? lengthEnc)
        {
            const string testString1 = "Hello, world!&*(@&*(fghjwgfwffgw";
            await ReadWriteStringUsingEncodingAsync(testString1, Encoding.UTF8, lengthEnc);
            await ReadWriteStringUsingEncodingAsync(testString1, Encoding.Unicode, lengthEnc);
            await ReadWriteStringUsingEncodingAsync(testString1, Encoding.UTF7, lengthEnc);
            await ReadWriteStringUsingEncodingAsync(testString1, Encoding.UTF32, lengthEnc);
            await ReadWriteStringUsingEncodingAsync(testString1, Encoding.ASCII, lengthEnc);
            const string testString2 = "������, ���!";
            await ReadWriteStringUsingEncodingAsync(testString2, Encoding.UTF8, lengthEnc);
            await ReadWriteStringUsingEncodingAsync(testString2, Encoding.Unicode, lengthEnc);
            await ReadWriteStringUsingEncodingAsync(testString2, Encoding.UTF7, lengthEnc);
            await ReadWriteStringUsingEncodingAsync(testString2, Encoding.UTF32, lengthEnc);
        }

        [Theory]
        [InlineData(10, null)]
        [InlineData(15, null)]
        [InlineData(1024, null)]
        [InlineData(10, StringLengthEncoding.Compressed)]
        [InlineData(15, StringLengthEncoding.Compressed)]
        [InlineData(1024, StringLengthEncoding.Compressed)]
        [InlineData(10, StringLengthEncoding.Plain)]
        [InlineData(15, StringLengthEncoding.Plain)]
        [InlineData(1024, StringLengthEncoding.Plain)]
        [InlineData(10, StringLengthEncoding.PlainLittleEndian)]
        [InlineData(15, StringLengthEncoding.PlainLittleEndian)]
        [InlineData(1024, StringLengthEncoding.PlainLittleEndian)]
        [InlineData(10, StringLengthEncoding.PlainBigEndian)]
        [InlineData(15, StringLengthEncoding.PlainBigEndian)]
        [InlineData(1024, StringLengthEncoding.PlainBigEndian)]
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
        public static void SynchronousCopying()
        {
            using var source = new MemoryStream(new byte[] { 2, 4, 5 });
            using var destination = new MemoryStream();
            var buffer = new byte[2];
            Equal(3L, source.CopyTo(destination, buffer));
        }

        [Fact]
        public static void ReadWriteBlittableType()
        {
            using var ms = new MemoryStream();
            ms.Write(10M);
            ms.Position = 0;
            Equal(10M, ms.Read<decimal>());
        }

        [Fact]
        public static async Task ReadWriteBlittableTypeUsingReader()
        {
            using var ms = new MemoryStream();
            ms.Write(10M);
            ms.Position = 0;
            var reader = IAsyncBinaryReader.Create(ms, new byte[128]);
            Equal(10M, await reader.ReadAsync<decimal>());
        }

        [Fact]
        public static async Task ReadWriteMemoryUsingReader()
        {
            using var ms = new MemoryStream();
            ms.Write(new byte[] { 1, 5, 7, 9 });
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
            using var ms = new MemoryStream();
            await ms.WriteAsync(10M);
            ms.Position = 0;
            Equal(10M, await ms.ReadAsync<decimal>());
        }

        [Fact]
        public static async Task BinaryReaderInterop()
        {
            using var ms = new MemoryStream();
            await ms.WriteStringAsync("ABC".AsMemory(), Encoding.UTF8, StringLengthEncoding.Compressed);
            ms.Position = 0;
            using var reader = new BinaryReader(ms, Encoding.UTF8, true);
            Equal("ABC", reader.ReadString());
        }

        [Fact]
        public static async Task BinaryWriterInterop()
        {
            using var ms = new MemoryStream();
            using (var writer = new BinaryWriter(ms, Encoding.UTF8, true))
            {
                writer.Write("ABC");
            }
            ms.Position = 0;
            Equal("ABC", await ms.ReadStringAsync(StringLengthEncoding.Compressed, Encoding.UTF8));
        }

        [Fact]
        public static void BufferWriterOverStream()
        {
            using var ms = new MemoryStream(256);
            var writer = ms.AsBufferWriter(ArrayPool<byte>.Shared.ToAllocator());
            var span = writer.GetSpan(4);
            span[0] = 1;
            span[1] = 2;
            span[2] = 3;
            span[3] = 4;
            writer.Advance(2);
            writer.Flush();
            Equal(new byte[] { 1, 2 }, ms.ToArray());
            writer.Advance(2);
            writer.FlushAsync().GetAwaiter().GetResult();
            Equal(new byte[] { 1, 2, 3, 4 }, ms.ToArray());
            writer.Advance(span.Length - 4);
            Equal(span.Length, ms.Length);
            span = writer.GetSpan(4);
            writer.Advance(4);
            Equal(span.Length + 4, ms.Length);
        }

        [Fact]
        public static void BufferWriterOverStreamExceptions()
        {
            Throws<ArgumentNullException>(() => StreamExtensions.AsBufferWriter(null, ArrayPool<byte>.Shared.ToAllocator()));
            Throws<ArgumentNullException>(() => Stream.Null.AsBufferWriter(null));
            using var ms = new MemoryStream(new byte[12], false);
            Throws<ArgumentException>(() => ms.AsBufferWriter(ArrayPool<byte>.Shared.ToAllocator()));
        }

        [Fact]
        public static void CopyToBufferWriter()
        {
            var writer = new ArrayBufferWriter<byte>();
            var bytes = new byte[] {0, 1, 2, 3, 4, 5, 6, 7, 8, 9};
            using var ms = new MemoryStream(bytes, false);
            Equal(10L, ms.CopyTo(writer, 3));
            Equal(bytes, writer.WrittenSpan.ToArray());
        }

        [Fact]
        public static async Task CopyToBufferWriterAsync()
        {
            var writer = new ArrayBufferWriter<byte>();
            var bytes = new byte[] {0, 1, 2, 3, 4, 5, 6, 7, 8, 9};
            using var ms = new MemoryStream(bytes, false);
            Equal(10L, await ms.CopyToAsync(writer, 3));
            Equal(bytes, writer.WrittenSpan.ToArray());
        }

        [Fact]
        public static void WriteSequence()
        {
            var content = new byte[] { 1, 5, 8, 9 };
            var sequence = new ChunkSequence<byte>(content, 2).ToReadOnlySequence();
            using var ms = new MemoryStream();
            ms.Write(sequence);
            ms.Position = 0;
            Equal(content, ms.ToArray());
        }
    }
}
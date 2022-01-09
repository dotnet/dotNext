using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text;
using static System.Globalization.CultureInfo;
using DateTimeStyles = System.Globalization.DateTimeStyles;

namespace DotNext.IO
{
    using Text;

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
            ReadStringUsingEncoding(testString1, Encoding.UTF32);
            ReadStringUsingEncoding(testString1, Encoding.ASCII);
            const string testString2 = "������, ���!";
            ReadStringUsingEncoding(testString2, Encoding.UTF8);
            ReadStringUsingEncoding(testString2, Encoding.Unicode);
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
            ReadStringUsingEncoding(testString1, Encoding.UTF32, bufferSize);
            ReadStringUsingEncoding(testString1, Encoding.ASCII, bufferSize);
            const string testString2 = "������, ���!";
            ReadStringUsingEncoding(testString2, Encoding.UTF8, bufferSize);
            ReadStringUsingEncoding(testString2, Encoding.Unicode, bufferSize);
            ReadStringUsingEncoding(testString2, Encoding.UTF32, bufferSize);
        }

        private static void ReadCharBufferUsingEncoding(string value, Encoding encoding, int bufferSize)
        {
            using var ms = new MemoryStream();
            ms.Write(encoding.GetBytes(value));
            ms.Position = 0;
            var buffer = new byte[bufferSize];
            using var chars = ms.ReadString(encoding.GetByteCount(value), encoding, buffer, null);
            Equal(value, new string(chars.Span));
        }

        private static void ReadCharBufferUsingEncoding(string value, Encoding encoding)
        {
            using var ms = new MemoryStream();
            ms.Write(encoding.GetBytes(value));
            ms.Position = 0;
            using var chars = ms.ReadString(encoding.GetByteCount(value), encoding, null);
            Equal(value, new string(chars.Span));
        }

        [Fact]
        public static void ReadCharsBufferWithoutByteBuffer()
        {
            const string testString1 = "Hello, world! &$@&@()&$YHWORww!";
            ReadCharBufferUsingEncoding(testString1, Encoding.UTF8);
            ReadCharBufferUsingEncoding(testString1, Encoding.Unicode);
            ReadCharBufferUsingEncoding(testString1, Encoding.UTF32);
            ReadCharBufferUsingEncoding(testString1, Encoding.ASCII);
            const string testString2 = "������, ���!";
            ReadCharBufferUsingEncoding(testString2, Encoding.UTF8);
            ReadCharBufferUsingEncoding(testString2, Encoding.Unicode);
            ReadCharBufferUsingEncoding(testString2, Encoding.UTF32);
        }

        [Theory]
        [InlineData(4)]
        [InlineData(15)]
        [InlineData(128)]
        public static void ReadCharsBuffer(int bufferSize)
        {
            const string testString1 = "Hello, world! &$@&@()&$YHWORww!";
            ReadCharBufferUsingEncoding(testString1, Encoding.UTF8, bufferSize);
            ReadCharBufferUsingEncoding(testString1, Encoding.Unicode, bufferSize);
            ReadCharBufferUsingEncoding(testString1, Encoding.UTF32, bufferSize);
            ReadCharBufferUsingEncoding(testString1, Encoding.ASCII, bufferSize);
            const string testString2 = "������, ���!";
            ReadCharBufferUsingEncoding(testString2, Encoding.UTF8, bufferSize);
            ReadCharBufferUsingEncoding(testString2, Encoding.Unicode, bufferSize);
            ReadCharBufferUsingEncoding(testString2, Encoding.UTF32, bufferSize);
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
            await ReadStringUsingEncodingAsync(testString1, Encoding.UTF32);
            await ReadStringUsingEncodingAsync(testString1, Encoding.ASCII);
            const string testString2 = "������, ���!";
            await ReadStringUsingEncodingAsync(testString2, Encoding.UTF8);
            await ReadStringUsingEncodingAsync(testString2, Encoding.Unicode);
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
            await ReadStringUsingEncodingAsync(testString1, Encoding.UTF32, bufferSize);
            await ReadStringUsingEncodingAsync(testString1, Encoding.ASCII, bufferSize);
            const string testString2 = "������, ���!";
            await ReadStringUsingEncodingAsync(testString2, Encoding.UTF8, bufferSize);
            await ReadStringUsingEncodingAsync(testString2, Encoding.Unicode, bufferSize);
            await ReadStringUsingEncodingAsync(testString2, Encoding.UTF32, bufferSize);
        }

        private static void ReadWriteStringUsingEncoding(Encoding encoding, int bufferSize, LengthFormat? lengthEnc)
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

        private static void ReadWriteStringUsingEncoding(string value, Encoding encoding, LengthFormat? lengthEnc)
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
        [InlineData(LengthFormat.Compressed)]
        [InlineData(LengthFormat.Plain)]
        [InlineData(LengthFormat.PlainBigEndian)]
        [InlineData(LengthFormat.PlainLittleEndian)]
        public static void ReadWriteString(LengthFormat? lengthEnc)
        {
            const string testString1 = "Hello, world!&*(@&*(fghjwgfwffgw";
            ReadWriteStringUsingEncoding(testString1, Encoding.UTF8, lengthEnc);
            ReadWriteStringUsingEncoding(testString1, Encoding.Unicode, lengthEnc);
            ReadWriteStringUsingEncoding(testString1, Encoding.UTF32, lengthEnc);
            ReadWriteStringUsingEncoding(testString1, Encoding.ASCII, lengthEnc);
            const string testString2 = "������, ���!";
            ReadWriteStringUsingEncoding(testString2, Encoding.UTF8, lengthEnc);
            ReadWriteStringUsingEncoding(testString2, Encoding.Unicode, lengthEnc);
            ReadWriteStringUsingEncoding(testString2, Encoding.UTF32, lengthEnc);
        }

        [Theory]
        [InlineData(10, null)]
        [InlineData(15, null)]
        [InlineData(1024, null)]
        [InlineData(10, LengthFormat.Plain)]
        [InlineData(15, LengthFormat.Plain)]
        [InlineData(1024, LengthFormat.Plain)]
        [InlineData(10, LengthFormat.Compressed)]
        [InlineData(15, LengthFormat.Compressed)]
        [InlineData(1024, LengthFormat.Compressed)]
        [InlineData(10, LengthFormat.PlainLittleEndian)]
        [InlineData(15, LengthFormat.PlainLittleEndian)]
        [InlineData(1024, LengthFormat.PlainLittleEndian)]
        [InlineData(10, LengthFormat.PlainBigEndian)]
        [InlineData(15, LengthFormat.PlainBigEndian)]
        [InlineData(1024, LengthFormat.PlainBigEndian)]
        public static void ReadWriteBufferedString(int bufferSize, LengthFormat? lengthEnc)
        {
            ReadWriteStringUsingEncoding(Encoding.UTF8, bufferSize, lengthEnc);
            ReadWriteStringUsingEncoding(Encoding.Unicode, bufferSize, lengthEnc);
            ReadWriteStringUsingEncoding(Encoding.UTF32, bufferSize, lengthEnc);
            ReadWriteStringUsingEncoding(Encoding.ASCII, bufferSize, lengthEnc);
        }

        private static async Task ReadWriteStringUsingEncodingAsync(string value, Encoding encoding, int bufferSize, LengthFormat? lengthEnc)
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

        private static async Task ReadWriteStringUsingEncodingAsync(string value, Encoding encoding, LengthFormat? lengthEnc)
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
        [InlineData(LengthFormat.Compressed)]
        [InlineData(LengthFormat.Plain)]
        [InlineData(LengthFormat.PlainLittleEndian)]
        [InlineData(LengthFormat.PlainBigEndian)]
        public static async Task ReadWriteStringAsync(LengthFormat? lengthEnc)
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
        [InlineData(10, null)]
        [InlineData(15, null)]
        [InlineData(1024, null)]
        [InlineData(10, LengthFormat.Compressed)]
        [InlineData(15, LengthFormat.Compressed)]
        [InlineData(1024, LengthFormat.Compressed)]
        [InlineData(10, LengthFormat.Plain)]
        [InlineData(15, LengthFormat.Plain)]
        [InlineData(1024, LengthFormat.Plain)]
        [InlineData(10, LengthFormat.PlainLittleEndian)]
        [InlineData(15, LengthFormat.PlainLittleEndian)]
        [InlineData(1024, LengthFormat.PlainLittleEndian)]
        [InlineData(10, LengthFormat.PlainBigEndian)]
        [InlineData(15, LengthFormat.PlainBigEndian)]
        [InlineData(1024, LengthFormat.PlainBigEndian)]
        public static async Task ReadWriteBufferedStringAsync(int bufferSize, LengthFormat? lengthEnc)
        {
            const string testString1 = "Hello, world!&*(@&*(fghjwgfwffgw";
            await ReadWriteStringUsingEncodingAsync(testString1, Encoding.UTF8, bufferSize, lengthEnc);
            await ReadWriteStringUsingEncodingAsync(testString1, Encoding.Unicode, bufferSize, lengthEnc);
            await ReadWriteStringUsingEncodingAsync(testString1, Encoding.UTF32, bufferSize, lengthEnc);
            await ReadWriteStringUsingEncodingAsync(testString1, Encoding.ASCII, bufferSize, lengthEnc);
            const string testString2 = "������, ���!";
            await ReadWriteStringUsingEncodingAsync(testString2, Encoding.UTF8, bufferSize, lengthEnc);
            await ReadWriteStringUsingEncodingAsync(testString2, Encoding.Unicode, bufferSize, lengthEnc);
            await ReadWriteStringUsingEncodingAsync(testString2, Encoding.UTF32, bufferSize, lengthEnc);
        }

        [Fact]
        public static void SynchronousCopying()
        {
            using var source = new MemoryStream(new byte[] { 2, 4, 5 });
            using var destination = new MemoryStream();
            var buffer = new byte[2];
            source.CopyTo(destination, buffer);
            Equal(3L, destination.Length);
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
            await ms.WriteStringAsync("ABC".AsMemory(), Encoding.UTF8, LengthFormat.Compressed);
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
            Equal("ABC", await ms.ReadStringAsync(LengthFormat.Compressed, Encoding.UTF8));
        }

        [Fact]
        public static void CopyToBufferWriter()
        {
            var writer = new ArrayBufferWriter<byte>();
            var bytes = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            using var ms = new MemoryStream(bytes, false);
            ms.CopyTo(writer, 3);
            Equal(10L, ms.Position);
            Equal(bytes, writer.WrittenSpan.ToArray());
        }

        [Fact]
        public static async Task CopyToBufferWriterAsync()
        {
            var writer = new ArrayBufferWriter<byte>();
            var bytes = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            using var ms = new MemoryStream(bytes, false);
            await ms.CopyToAsync(writer, 3);
            Equal(10L, ms.Position);
            Equal(bytes, writer.WrittenSpan.ToArray());
        }

        [Fact]
        public static void WriteSequence()
        {
            var content = new byte[] { 1, 5, 8, 9 };
            var sequence = ToReadOnlySequence<byte>(content, 2);
            using var ms = new MemoryStream();
            ms.Write(sequence);
            ms.Position = 0;
            Equal(content, ms.ToArray());
        }

        [Theory]
        [InlineData("UTF-8")]
        [InlineData("UTF-16")]
        public static void EncodeAsString(string encodingName)
        {
            var encoding = Encoding.GetEncoding(encodingName);
            using var stream = new MemoryStream();
            var g = Guid.NewGuid();
            var dt = DateTime.Now;
            var dto = DateTimeOffset.Now;
            var t = TimeSpan.FromMilliseconds(1096);
            var blob = RandomBytes(128);
            var bi = new BigInteger(RandomBytes(64));
            var memberId = new Net.Cluster.ClusterMemberId(Random.Shared);

            stream.WriteFormattable<long>(42L, LengthFormat.Plain, encoding, provider: InvariantCulture);
            stream.WriteFormattable<ulong>(12UL, LengthFormat.PlainLittleEndian, encoding, provider: InvariantCulture);
            stream.WriteFormattable<int>(34, LengthFormat.PlainBigEndian, encoding, provider: InvariantCulture);
            stream.WriteFormattable<uint>(78, LengthFormat.Plain, encoding, provider: InvariantCulture);
            stream.WriteFormattable<short>(90, LengthFormat.Plain, encoding, provider: InvariantCulture);
            stream.WriteFormattable<ushort>(12, LengthFormat.Plain, encoding, format: "X", provider: InvariantCulture);
            stream.WriteFormattable<ushort>(12, LengthFormat.Plain, encoding, provider: InvariantCulture);
            stream.WriteFormattable<byte>(10, LengthFormat.Plain, encoding, format: "X", provider: InvariantCulture);
            stream.WriteFormattable<sbyte>(11, LengthFormat.Plain, encoding, format: "X", provider: InvariantCulture);
            stream.WriteFormattable<byte>(10, LengthFormat.Plain, encoding, provider: InvariantCulture);
            stream.WriteFormattable<sbyte>(11, LengthFormat.Plain, encoding, provider: InvariantCulture);
            stream.WriteFormattable(g, LengthFormat.Plain, encoding);
            stream.WriteFormattable(g, LengthFormat.Plain, encoding, format: "X");
            stream.WriteFormattable(dt, LengthFormat.Plain, encoding, format: "O", provider: InvariantCulture);
            stream.WriteFormattable(dto, LengthFormat.Plain, encoding, format: "O", provider: InvariantCulture);
            stream.WriteFormattable(dt, LengthFormat.Plain, encoding, format: "O", provider: InvariantCulture);
            stream.WriteFormattable(dto, LengthFormat.Plain, encoding, format: "O", provider: InvariantCulture);
            stream.WriteFormattable<decimal>(42.5M, LengthFormat.Plain, encoding, provider: InvariantCulture);
            stream.WriteFormattable<float>(32.2F, LengthFormat.Plain, encoding, provider: InvariantCulture);
            stream.WriteFormattable<double>(56.6D, LengthFormat.Plain, encoding, provider: InvariantCulture);
            stream.WriteFormattable(t, LengthFormat.Plain, encoding, provider: InvariantCulture);
            stream.WriteFormattable(t, LengthFormat.Plain, encoding, "G", provider: InvariantCulture);
            stream.WriteBlock(blob, LengthFormat.Plain);
            stream.WriteFormattable(bi, LengthFormat.Plain, encoding, provider: InvariantCulture);
            stream.WriteFormattable(memberId);

            stream.Position = 0;
            DecodingContext decodingContext = encoding;
            Span<byte> buffer = stackalloc byte[256];
            Equal(42L, stream.Parse<long>(static (c, p) => long.Parse(c, provider: p), LengthFormat.Plain, in decodingContext, buffer, provider: InvariantCulture));
            Equal(12UL, stream.Parse<ulong>(static (c, p) => ulong.Parse(c, provider: p), LengthFormat.PlainLittleEndian, in decodingContext, buffer, provider: InvariantCulture));
            Equal(34, stream.Parse<int>(static (c, p) => int.Parse(c, provider: p), LengthFormat.PlainBigEndian, in decodingContext, buffer, provider: InvariantCulture));
            Equal(78U, stream.Parse<uint>(static (c, p) => uint.Parse(c, provider: p), LengthFormat.Plain, in decodingContext, buffer, provider: InvariantCulture));
            Equal(90, stream.Parse<short>(static (c, p) => short.Parse(c, provider: p), LengthFormat.Plain, in decodingContext, buffer, provider: InvariantCulture));
            Equal("C", stream.ReadString(LengthFormat.Plain, in decodingContext, buffer));
            Equal(12, stream.Parse<ushort>(static (c, p) => ushort.Parse(c, provider: p), LengthFormat.Plain, in decodingContext, buffer, provider: InvariantCulture));
            Equal("A", stream.ReadString(LengthFormat.Plain, in decodingContext, buffer));
            Equal("B", stream.ReadString(LengthFormat.Plain, in decodingContext, buffer));
            Equal(10, stream.Parse<byte>(static (c, p) => byte.Parse(c, provider: p), LengthFormat.Plain, in decodingContext, buffer, provider: InvariantCulture));
            Equal(11, stream.Parse<sbyte>(static (c, p) => sbyte.Parse(c, provider: p), LengthFormat.Plain, in decodingContext, buffer, provider: InvariantCulture));
            Equal(g, stream.Parse<Guid>(static (c, p) => Guid.Parse(c), LengthFormat.Plain, in decodingContext, buffer));
            Equal(g, stream.Parse<Guid>(static (c, p) => Guid.ParseExact(c, "X"), LengthFormat.Plain, in decodingContext, buffer));
            Equal(dt, stream.Parse<DateTime>(static (c, p) => DateTime.Parse(c, p, DateTimeStyles.RoundtripKind), LengthFormat.Plain, in decodingContext, buffer, provider: InvariantCulture));
            Equal(dto, stream.Parse<DateTimeOffset>(static (c, p) => DateTimeOffset.Parse(c, p, DateTimeStyles.RoundtripKind), LengthFormat.Plain, in decodingContext, buffer, provider: InvariantCulture));
            Equal(dt, stream.Parse<DateTime>(static (c, p) => DateTime.ParseExact(c, "O", p, DateTimeStyles.RoundtripKind), LengthFormat.Plain, in decodingContext, buffer, provider: InvariantCulture));
            Equal(dto, stream.Parse<DateTimeOffset>(static (c, p) => DateTime.ParseExact(c, "O", p, DateTimeStyles.RoundtripKind), LengthFormat.Plain, in decodingContext, buffer, provider: InvariantCulture));
            Equal(42.5M, stream.Parse<decimal>(static (c, p) => decimal.Parse(c, provider: p), LengthFormat.Plain, in decodingContext, buffer, provider: InvariantCulture));
            Equal(32.2F, stream.Parse<float>(static (c, p) => float.Parse(c, provider: p), LengthFormat.Plain, in decodingContext, buffer, provider: InvariantCulture));
            Equal(56.6D, stream.Parse<double>(static (c, p) => double.Parse(c, provider: p), LengthFormat.Plain, in decodingContext, buffer, provider: InvariantCulture));
            Equal(t, stream.Parse<TimeSpan>(TimeSpan.Parse, LengthFormat.Plain, in decodingContext, buffer, provider: InvariantCulture));
            Equal(t, stream.Parse<TimeSpan>(static (c, p) => TimeSpan.ParseExact(c, "G", p), LengthFormat.Plain, in decodingContext, buffer, provider: InvariantCulture));
            using var decodedBlob = stream.ReadBlock(LengthFormat.Plain);
            Equal(blob, decodedBlob.Memory.ToArray());
            Equal(bi, stream.Parse<BigInteger>(static (c, p) => BigInteger.Parse(c, provider: p), LengthFormat.Plain, in decodingContext, buffer, provider: InvariantCulture));
            Equal(memberId, stream.Parse<Net.Cluster.ClusterMemberId>());
        }

        [Theory]
        [InlineData("UTF-8")]
        [InlineData("UTF-16")]
        public static async Task EncodeAsStringAsync(string encodingName)
        {
            var encoding = Encoding.GetEncoding(encodingName);
            using var stream = new MemoryStream();
            var g = Guid.NewGuid();
            var dt = DateTime.Now;
            var dto = DateTimeOffset.Now;
            var t = TimeSpan.FromMilliseconds(1096);
            var bi = new BigInteger(RandomBytes(64));
            var memberId = new Net.Cluster.ClusterMemberId(Random.Shared);

            await stream.WriteFormattableAsync<long>(42L, LengthFormat.Plain, encoding, provider: InvariantCulture);
            await stream.WriteFormattableAsync<ulong>(12UL, LengthFormat.PlainLittleEndian, encoding, provider: InvariantCulture);
            await stream.WriteFormattableAsync<int>(34, LengthFormat.PlainBigEndian, encoding, provider: InvariantCulture);
            await stream.WriteFormattableAsync<uint>(78U, LengthFormat.Plain, encoding, provider: InvariantCulture);
            await stream.WriteFormattableAsync<short>(90, LengthFormat.Plain, encoding, provider: InvariantCulture);
            await stream.WriteFormattableAsync<ushort>(12, LengthFormat.Plain, encoding, format: "X", provider: InvariantCulture);
            await stream.WriteFormattableAsync<ushort>(12, LengthFormat.Plain, encoding, provider: InvariantCulture);
            await stream.WriteFormattableAsync<byte>(10, LengthFormat.Plain, encoding, format: "X", provider: InvariantCulture);
            await stream.WriteFormattableAsync<sbyte>(11, LengthFormat.Plain, encoding, format: "X", provider: InvariantCulture);
            await stream.WriteFormattableAsync<byte>(10, LengthFormat.Plain, encoding, provider: InvariantCulture);
            await stream.WriteFormattableAsync<sbyte>(11, LengthFormat.Plain, encoding, provider: InvariantCulture);
            await stream.WriteFormattableAsync(g, LengthFormat.Plain, encoding);
            await stream.WriteFormattableAsync(g, LengthFormat.Plain, encoding, format: "X");
            await stream.WriteFormattableAsync(dt, LengthFormat.Plain, encoding, format: "O", provider: InvariantCulture);
            await stream.WriteFormattableAsync(dto, LengthFormat.Plain, encoding, format: "O", provider: InvariantCulture);
            await stream.WriteFormattableAsync(dt, LengthFormat.Plain, encoding, format: "O", provider: InvariantCulture);
            await stream.WriteFormattableAsync(dto, LengthFormat.Plain, encoding, format: "O", provider: InvariantCulture);
            await stream.WriteFormattableAsync<decimal>(42.5M, LengthFormat.Plain, encoding, provider: InvariantCulture);
            await stream.WriteFormattableAsync<float>(32.2F, LengthFormat.Plain, encoding, provider: InvariantCulture);
            await stream.WriteFormattableAsync<double>(56.6D, LengthFormat.Plain, encoding, provider: InvariantCulture);
            await stream.WriteFormattableAsync(t, LengthFormat.Plain, encoding, provider: InvariantCulture);
            await stream.WriteFormattableAsync(t, LengthFormat.Plain, encoding, "G", provider: InvariantCulture);
            await stream.WriteFormattableAsync(bi, LengthFormat.Plain, encoding, provider: InvariantCulture);
            await stream.WriteFormattableAsync(memberId);

            stream.Position = 0;
            DecodingContext decodingContext = encoding;
            Equal(42L, await stream.ParseAsync<long>(static (c, p) => long.Parse(c, provider: p), LengthFormat.Plain, decodingContext, provider: InvariantCulture));
            Equal(12UL, await stream.ParseAsync<ulong>(static (c, p) => ulong.Parse(c, provider: p), LengthFormat.PlainLittleEndian, decodingContext, provider: InvariantCulture));
            Equal(34, await stream.ParseAsync<int>(static (c, p) => int.Parse(c, provider: p), LengthFormat.PlainBigEndian, decodingContext, provider: InvariantCulture));
            Equal(78U, await stream.ParseAsync<uint>(static (c, p) => uint.Parse(c, provider: p), LengthFormat.Plain, decodingContext, provider: InvariantCulture));
            Equal(90, await stream.ParseAsync<short>(static (c, p) => short.Parse(c, provider: p), LengthFormat.Plain, decodingContext, provider: InvariantCulture));
            Equal("C", await stream.ReadStringAsync(LengthFormat.Plain, decodingContext.Encoding));
            Equal(12, await stream.ParseAsync<ushort>(static (c, p) => ushort.Parse(c, provider: p), LengthFormat.Plain, decodingContext, provider: InvariantCulture));
            Equal("A", await stream.ReadStringAsync(LengthFormat.Plain, decodingContext.Encoding));
            Equal("B", await stream.ReadStringAsync(LengthFormat.Plain, decodingContext.Encoding));
            Equal(10, await stream.ParseAsync<byte>(static (c, p) => byte.Parse(c, provider: p), LengthFormat.Plain, decodingContext, provider: InvariantCulture));
            Equal(11, await stream.ParseAsync<sbyte>(static (c, p) => sbyte.Parse(c, provider: p), LengthFormat.Plain, decodingContext, provider: InvariantCulture));
            Equal(g, await stream.ParseAsync<Guid>(static (c, p) => Guid.Parse(c), LengthFormat.Plain, decodingContext));
            Equal(g, await stream.ParseAsync<Guid>(static (c, p) => Guid.ParseExact(c, "X"), LengthFormat.Plain, decodingContext, provider: InvariantCulture));
            Equal(dt, await stream.ParseAsync<DateTime>(static (c, p) => DateTime.Parse(c, p, DateTimeStyles.RoundtripKind), LengthFormat.Plain, decodingContext, provider: InvariantCulture));
            Equal(dto, await stream.ParseAsync<DateTimeOffset>(static (c, p) => DateTimeOffset.Parse(c, p, DateTimeStyles.RoundtripKind), LengthFormat.Plain, decodingContext, provider: InvariantCulture));
            Equal(dt, await stream.ParseAsync<DateTime>(static (c, p) => DateTime.ParseExact(c, "O", p, DateTimeStyles.RoundtripKind), LengthFormat.Plain, decodingContext, provider: InvariantCulture));
            Equal(dto, await stream.ParseAsync<DateTimeOffset>(static (c, p) => DateTimeOffset.ParseExact(c, "O", p, DateTimeStyles.RoundtripKind), LengthFormat.Plain, decodingContext, provider: InvariantCulture));
            Equal(42.5M, await stream.ParseAsync<decimal>(static (c, p) => decimal.Parse(c, provider: p), LengthFormat.Plain, decodingContext, provider: InvariantCulture));
            Equal(32.2F, await stream.ParseAsync<float>(static (c, p) => float.Parse(c, provider: p), LengthFormat.Plain, decodingContext, provider: InvariantCulture));
            Equal(56.6D, await stream.ParseAsync<double>(static (c, p) => double.Parse(c, provider: p), LengthFormat.Plain, decodingContext, provider: InvariantCulture));
            Equal(t, await stream.ParseAsync<TimeSpan>(TimeSpan.Parse, LengthFormat.Plain, decodingContext, provider: InvariantCulture));
            Equal(t, await stream.ParseAsync<TimeSpan>(static (c, p) => TimeSpan.ParseExact(c, "G", p), LengthFormat.Plain, decodingContext, provider: InvariantCulture));
            Equal(bi, await stream.ParseAsync<BigInteger>(static (c, p) => BigInteger.Parse(c, provider: p), LengthFormat.Plain, decodingContext, provider: InvariantCulture));
            Equal(memberId, await stream.ParseAsync<Net.Cluster.ClusterMemberId>());
        }

        [Fact]
        public static void CombineStreams()
        {
            using var ms1 = new MemoryStream(new byte[] { 1, 2, 3 });
            using var ms2 = new MemoryStream(new byte[] { 4, 5, 6 });
            using var combined = ms1.Combine(ms2);
            True(combined.CanRead);
            False(combined.CanWrite);
            False(combined.CanSeek);

            Span<byte> buffer = stackalloc byte[6];
            combined.ReadBlock(buffer);

            Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, buffer.ToArray());
        }

        [Fact]
        public static async Task CombineStreamsAsync()
        {
            await using var ms1 = new MemoryStream(new byte[] { 1, 2, 3 });
            await using var ms2 = new MemoryStream(new byte[] { 4, 5, 6 });
            await using var combined = new[] { ms1, ms2 }.Combine();

            var buffer = new byte[6];
            await combined.ReadBlockAsync(buffer);

            Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, buffer);
        }

        [Fact]
        public static void CopyCombinedStreams()
        {
            using var ms1 = new MemoryStream(new byte[] { 1, 2, 3 });
            using var ms2 = new MemoryStream(new byte[] { 4, 5, 6 });
            using var combined = ms1.Combine(ms2);
            using var result = new MemoryStream();

            combined.CopyTo(result, 128);
            Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, result.ToArray());
        }

        [Fact]
        public static async Task CopyCombinedStreamsAsync()
        {
            await using var ms1 = new MemoryStream(new byte[] { 1, 2, 3 });
            await using var ms2 = new MemoryStream(new byte[] { 4, 5, 6 });
            await using var combined = ms1.Combine(ms2);
            await using var result = new MemoryStream();

            await combined.CopyToAsync(result, 128);
            Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, result.ToArray());
        }

        [Fact]
        public static void ReadBytesFromCombinedStream()
        {
            using var ms1 = new MemoryStream(new byte[] { 1, 2, 3 });
            using var ms2 = new MemoryStream(new byte[] { 4, 5, 6 });
            using var combined = ms1.Combine(ms2);

            Equal(1, combined.ReadByte());
            Equal(2, combined.ReadByte());
            Equal(3, combined.ReadByte());
            Equal(4, combined.ReadByte());
            Equal(5, combined.ReadByte());
            Equal(6, combined.ReadByte());
            Equal(-1, combined.ReadByte());
        }

        [Fact]
        public static async Task UnsupportedMethodsOfSparseStream()
        {
            await using var ms1 = new MemoryStream(new byte[] { 1, 2, 3 });
            await using var ms2 = new MemoryStream(new byte[] { 4, 5, 6 });
            await using var combined = ms1.Combine(ms2);

            Throws<NotSupportedException>(() => combined.SetLength(0L));
            Throws<NotSupportedException>(() => combined.Seek(0L, default));
            Throws<NotSupportedException>(() => combined.Position.ToString());
            Throws<NotSupportedException>(() => combined.Position = 42L);
            Throws<NotSupportedException>(() => combined.WriteByte(1));
            Throws<NotSupportedException>(() => combined.Write(ReadOnlySpan<byte>.Empty));
            await ThrowsAsync<NotSupportedException>(async () => await combined.WriteAsync(ReadOnlyMemory<byte>.Empty));
        }

        [Fact]
        public static void ReadAtLeastBytes()
        {
            using var ms = new MemoryStream(new byte[] { 1, 2, 3, 4 });
            Span<byte> buffer = stackalloc byte[4];
            Equal(4, ms.ReadAtLeast(3, buffer));
            Equal(ms.ToArray(), buffer.ToArray());
        }

        [Fact]
        public static async Task ReadAtLeastBytesAsync()
        {
            using var ms = new MemoryStream(new byte[] { 1, 2, 3, 4 });
            var buffer = new byte[4];
            Equal(4, await ms.ReadAtLeastAsync(3, buffer));
            Equal(ms.ToArray(), buffer.ToArray());
        }
    }
}
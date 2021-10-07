using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Text;
using static System.Globalization.CultureInfo;

namespace DotNext.Buffers
{
    using Text;
    using IAsyncBinaryReader = IO.IAsyncBinaryReader;
    using LengthFormat = IO.LengthFormat;

    [ExcludeFromCodeCoverage]
    public sealed class BufferWriterTests : Test
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public static async Task ReadBlittableTypes(bool littleEndian)
        {
            var bi = new BigInteger(RandomBytes(32));
            var writer = new ArrayBufferWriter<byte>();
            writer.Write(10M);
            writer.WriteInt64(42L, littleEndian);
            writer.WriteInt32(44, littleEndian);
            writer.WriteInt16(46, littleEndian);
            writer.WriteBigInteger(in bi, littleEndian, LengthFormat.Compressed);

            IAsyncBinaryReader reader = IAsyncBinaryReader.Create(writer.WrittenMemory);
            Equal(10M, await reader.ReadAsync<decimal>());
            Equal(42L, await reader.ReadInt64Async(littleEndian));
            Equal(44, await reader.ReadInt32Async(littleEndian));
            Equal(46, await reader.ReadInt16Async(littleEndian));
            Equal(bi, await reader.ReadBigIntegerAsync(LengthFormat.Compressed, littleEndian));
        }

        private static async Task ReadWriteStringUsingEncodingAsync(string value, Encoding encoding, int bufferSize, LengthFormat? lengthEnc)
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
        [InlineData(0, LengthFormat.Compressed)]
        [InlineData(0, LengthFormat.Plain)]
        [InlineData(0, LengthFormat.PlainLittleEndian)]
        [InlineData(0, LengthFormat.PlainBigEndian)]
        [InlineData(128, null)]
        [InlineData(128, LengthFormat.Compressed)]
        [InlineData(128, LengthFormat.Plain)]
        [InlineData(128, LengthFormat.PlainLittleEndian)]
        [InlineData(128, LengthFormat.PlainBigEndian)]
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
                writer.WriteFormattable<int>(42, provider: InvariantCulture);
                writer.WriteFormattable<uint>(56U, provider: InvariantCulture);
                writer.WriteFormattable<byte>(10, provider: InvariantCulture);
                writer.WriteFormattable<sbyte>(22, provider: InvariantCulture);
                writer.WriteFormattable<short>(88, provider: InvariantCulture);
                writer.WriteFormattable<ushort>(99, provider: InvariantCulture);
                writer.WriteFormattable<long>(77L, provider: InvariantCulture);
                writer.WriteFormattable<ulong>(66UL, provider: InvariantCulture);

                var guid = Guid.NewGuid();
                writer.WriteFormattable(guid);

                var dt = DateTime.Now;
                writer.WriteFormattable(dt, provider: InvariantCulture);

                var dto = DateTimeOffset.Now;
                writer.WriteFormattable(dto, provider: InvariantCulture);

                writer.WriteFormattable<decimal>(42.5M, provider: InvariantCulture);
                writer.WriteFormattable<float>(32.2F, provider: InvariantCulture);
                writer.WriteFormattable<double>(56.6D, provider: InvariantCulture);

                Equal("Hello, world!!!" + Environment.NewLine + "4256102288997766" + guid + dt.ToString(InvariantCulture) + dto.ToString(InvariantCulture) + "42.532.256.6", writer.ToString());
            }
        }

        public static IEnumerable<object[]> ByteWriters()
        {
            yield return new object[] { new PooledBufferWriter<byte>(MemoryPool<byte>.Shared.ToAllocator()), Encoding.UTF32 };
            yield return new object[] { new PooledArrayBufferWriter<byte>(), Encoding.UTF8 };
        }

        [Theory]
        [MemberData(nameof(ByteWriters))]
        public static void EncodeAsString(BufferWriter<byte> writer, Encoding encoding)
        {
            var encodingContext = new EncodingContext(encoding, true);
            using (writer)
            {
                var g = Guid.NewGuid();
                var bi = new BigInteger(RandomBytes(64));
                var dt = DateTime.Now;
                var dto = DateTimeOffset.Now;
                writer.WriteFormattable<long>(42L, LengthFormat.Plain, in encodingContext, provider: InvariantCulture);
                writer.WriteFormattable<ulong>(12UL, LengthFormat.PlainLittleEndian, in encodingContext, provider: InvariantCulture);
                writer.WriteFormattable<int>(34, LengthFormat.PlainBigEndian, in encodingContext, provider: InvariantCulture);
                writer.WriteFormattable<uint>(78, LengthFormat.Plain, in encodingContext, provider: InvariantCulture);
                writer.WriteFormattable<short>(90, LengthFormat.Plain, in encodingContext, provider: InvariantCulture);
                writer.WriteFormattable<ushort>(12, LengthFormat.Plain, in encodingContext, format: "X", provider: InvariantCulture);
                writer.WriteFormattable<ushort>(12, LengthFormat.Plain, in encodingContext, provider: InvariantCulture);
                writer.WriteFormattable<byte>(10, LengthFormat.Plain, in encodingContext, format: "X", provider: InvariantCulture);
                writer.WriteFormattable<sbyte>(11, LengthFormat.Plain, in encodingContext, format: "X", provider: InvariantCulture);
                writer.WriteFormattable<byte>(10, LengthFormat.Plain, in encodingContext, provider: InvariantCulture);
                writer.WriteFormattable<sbyte>(11, LengthFormat.Plain, in encodingContext, provider: InvariantCulture);
                writer.WriteFormattable(g, LengthFormat.Plain, in encodingContext);
                writer.WriteFormattable(g, LengthFormat.Plain, in encodingContext, format: "X");
                writer.WriteFormattable(dt, LengthFormat.Plain, in encodingContext, format: "O", provider: InvariantCulture);
                writer.WriteFormattable(dto, LengthFormat.Plain, in encodingContext, format: "O", provider: InvariantCulture);
                writer.WriteFormattable(dt, LengthFormat.Plain, in encodingContext, format: "O", provider: InvariantCulture);
                writer.WriteFormattable(dto, LengthFormat.Plain, in encodingContext, format: "O", provider: InvariantCulture);
                writer.WriteFormattable(42.5M, LengthFormat.Plain, in encodingContext, provider: InvariantCulture);
                writer.WriteFormattable<float>(32.2F, LengthFormat.Plain, in encodingContext, provider: InvariantCulture);
                writer.WriteFormattable<double>(56.6D, LengthFormat.Plain, in encodingContext, provider: InvariantCulture);
                writer.WriteFormattable(bi, LengthFormat.Plain, in encodingContext, provider: InvariantCulture);

                var decodingContext = new DecodingContext(encoding, true);
                var reader = IAsyncBinaryReader.Create(writer.WrittenMemory);
                Equal(42L, reader.Parse<long>(static (c, p) => long.Parse(c, provider: p), LengthFormat.Plain, in decodingContext, provider: InvariantCulture));
                Equal(12UL, reader.Parse<ulong>(static (c, p) => ulong.Parse(c, provider: p), LengthFormat.PlainLittleEndian, in decodingContext, provider: InvariantCulture));
                Equal(34, reader.Parse<int>(static (c, p) => int.Parse(c, provider: p), LengthFormat.PlainBigEndian, in decodingContext, provider: InvariantCulture));
                Equal(78U, reader.Parse<uint>(static(c, p) => uint.Parse(c, provider: p), LengthFormat.Plain, in decodingContext, provider: InvariantCulture));
                Equal(90, reader.Parse<short>(static (c, p) => short.Parse(c, provider: p), LengthFormat.Plain, in decodingContext, provider: InvariantCulture));
                Equal("C", reader.ReadString(LengthFormat.Plain, in decodingContext));
                Equal(12, reader.Parse<ushort>(static (c, p) => ushort.Parse(c, provider: p), LengthFormat.Plain, in decodingContext, provider: InvariantCulture));
                Equal("A", reader.ReadString(LengthFormat.Plain, in decodingContext));
                Equal("B", reader.ReadString(LengthFormat.Plain, in decodingContext));
                Equal(10, reader.Parse<byte>(static (c, p) => byte.Parse(c, provider: p), LengthFormat.Plain, in decodingContext, provider: InvariantCulture));
                Equal(11, reader.Parse<sbyte>(static (c, p) => sbyte.Parse(c, provider: p), LengthFormat.Plain, in decodingContext, provider: InvariantCulture));
                Equal(g, reader.Parse<Guid>(static (c, p) => Guid.Parse(c), LengthFormat.Plain, in decodingContext));
                Equal(g, reader.Parse<Guid>(static (c, p) => Guid.ParseExact(c, "X"), LengthFormat.Plain, in decodingContext));
                Equal(dt, reader.Parse<DateTime>(static (c, p) => DateTime.Parse(c, p, DateTimeStyles.RoundtripKind), LengthFormat.Plain, in decodingContext, provider: InvariantCulture));
                Equal(dto, reader.Parse<DateTimeOffset>(static (c, p) => DateTimeOffset.Parse(c, p, DateTimeStyles.RoundtripKind), LengthFormat.Plain, in decodingContext, provider: InvariantCulture));
                Equal(dt, reader.Parse<DateTime>(static (c, p) => DateTime.ParseExact(c, "O", p, DateTimeStyles.RoundtripKind), LengthFormat.Plain, in decodingContext, provider: InvariantCulture));
                Equal(dto, reader.Parse<DateTimeOffset>(static (c, p) => DateTimeOffset.ParseExact(c, "O", p, DateTimeStyles.RoundtripKind), LengthFormat.Plain, in decodingContext, provider: InvariantCulture));
                Equal(42.5M, reader.Parse<decimal>(static (c, p) => decimal.Parse(c, provider: p), LengthFormat.Plain, in decodingContext, provider: InvariantCulture));
                Equal(32.2F, reader.Parse<float>(static (c, p) => float.Parse(c, provider: p), LengthFormat.Plain, in decodingContext, provider: InvariantCulture));
                Equal(56.6D, reader.Parse<double>(static (c, p) => double.Parse(c, provider: p), LengthFormat.Plain, in decodingContext, provider: InvariantCulture));
                Equal(bi, reader.Parse<BigInteger>(static (c, p) => BigInteger.Parse(c, provider: p), LengthFormat.Plain, in decodingContext, provider: InvariantCulture));
            }
        }

        [Fact]
        public static void FormatValues()
        {
            using var writer = new PooledArrayBufferWriter<char>(64);

            const string expectedString = "Hello, world!";
            Equal(expectedString.Length, writer.WriteAsString(expectedString));
            Equal(expectedString, writer.ToString());
            writer.Clear();

            Equal(2, writer.WriteAsString(56, provider: InvariantCulture));
            Equal("56", writer.ToString());
        }

        public static IEnumerable<object[]> ContiguousBuffers()
        {
            yield return new object[] { new PooledBufferWriter<byte>() };
            yield return new object[] { new PooledArrayBufferWriter<byte>() };
        }

        [Theory]
        [MemberData(nameof(ContiguousBuffers))]
        public static void DetachBuffer(BufferWriter<byte> writer)
        {
            using (writer)
            {
                var buffer = writer.DetachBuffer();
                True(buffer.IsEmpty);
                var bytes = new byte[] { 10, 20, 30 };
                writer.Write(bytes);
                Equal(3, writer.WrittenCount);
                buffer = writer.DetachBuffer();
                Equal(0, writer.WrittenCount);
                Equal(0, writer.FreeCapacity);
                False(buffer.IsEmpty);
                Equal(3, buffer.Length);
                Equal(bytes, buffer.Memory.ToArray());
                buffer.Dispose();
            }
        }

        [Theory]
        [InlineData(10, 10)]
        [InlineData(int.MaxValue, int.MinValue)]
        public static void WriteInterpolatedStringToBufferWriter(int x, int y)
        {
            using var buffer = new PooledArrayBufferWriter<char>();

            buffer.Write($"{x,4:X} = {y,-3:X}");
            Equal($"{x,4:X} = {y,-3:X}", buffer.ToString());
        }

        [Theory]
        [InlineData(10, 10)]
        [InlineData(int.MaxValue, int.MinValue)]
        public static async Task WriteInterpolatedStringToBufferWriterAsync(int x, int y)
        {
            var xt = Task.FromResult<int>(x);
            var yt = Task.FromResult<int>(y);

            using var buffer = new PooledArrayBufferWriter<char>();
            buffer.WriteString($"{await xt,4:X} = {await yt,-3:X}");
            Equal($"{x,4:X} = {y,-3:X}", buffer.ToString());
        }

        [Theory]
        [InlineData(10, 10)]
        [InlineData(int.MaxValue, int.MinValue)]
        public static void WriteInterpolatedStringToBufferWriterSlim(int x, int y)
        {
            var buffer = new BufferWriterSlim<char>(stackalloc char[4]);

            try
            {
                buffer.WriteString($"{x,4:X} = {y,-3:X}");
                Equal($"{x,4:X} = {y,-3:X}", buffer.ToString());
            }
            finally
            {
                buffer.Dispose();
            }
        }

        [Theory]
        [InlineData(0, "UTF-8", 10, 10)]
        [InlineData(0, "UTF-8", int.MaxValue, int.MinValue)]
        [InlineData(0, "UTF-16LE", 10, 10)]
        [InlineData(0, "UTF-16BE", int.MaxValue, int.MinValue)]
        [InlineData(0, "UTF-32LE", 10, 10)]
        [InlineData(0, "UTF-32BE", int.MaxValue, int.MinValue)]
        [InlineData(8, "UTF-8", 10, 10)]
        [InlineData(8, "UTF-8", int.MaxValue, int.MinValue)]
        [InlineData(8, "UTF-16LE", 10, 10)]
        [InlineData(8, "UTF-16BE", int.MaxValue, int.MinValue)]
        [InlineData(8, "UTF-32LE", 10, 10)]
        [InlineData(8, "UTF-32BE", int.MaxValue, int.MinValue)]
        public static void EncodeInterpolatedString(int bufferSize, string encoding, int x, int y)
        {
            var writer = new ArrayBufferWriter<byte>();
            Span<char> buffer = stackalloc char[bufferSize];

            var context = new EncodingContext(Encoding.GetEncoding(encoding), true);
            True(writer.WriteString(in context, buffer, null, $"{x,4:X} = {y,-3:X}") > 0);

            Equal($"{x,4:X} = {y,-3:X}", context.Encoding.GetString(writer.WrittenSpan));
        }
    }
}
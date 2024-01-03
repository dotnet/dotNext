using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text;
using static System.Globalization.CultureInfo;

namespace DotNext.Buffers;

using DecodingContext = DotNext.Text.DecodingContext;
using EncodingContext = DotNext.Text.EncodingContext;
using IAsyncBinaryReader = IO.IAsyncBinaryReader;
using LengthFormat = IO.LengthFormat;

public sealed class BufferWriterTests : Test
{
    [Fact]
    public static async Task ReadBlittableTypes()
    {
        var writer = new ArrayBufferWriter<byte>();
        writer.WriteLittleEndian(42L);
        writer.WriteLittleEndian(44);
        writer.WriteLittleEndian<short>(46);

        IAsyncBinaryReader reader = IAsyncBinaryReader.Create(writer.WrittenMemory);
        Equal(42L, await reader.ReadLittleEndianAsync<long>());
        Equal(44, await reader.ReadLittleEndianAsync<int>());
        Equal(46, await reader.ReadLittleEndianAsync<short>());
    }

    private static async Task ReadWriteStringUsingEncodingAsync(string value, Encoding encoding, LengthFormat lengthEnc)
    {
        var writer = new ArrayBufferWriter<byte>();
        writer.Encode(value.AsSpan(), encoding, lengthEnc);
        IAsyncBinaryReader reader = IAsyncBinaryReader.Create(writer.WrittenMemory);
        using var buffer = await reader.DecodeAsync(encoding, lengthEnc);
        Equal(value, buffer.ToString());
    }

    [Theory]
    [InlineData(LengthFormat.Compressed)]
    [InlineData(LengthFormat.LittleEndian)]
    [InlineData(LengthFormat.BigEndian)]
    public static async Task ReadWriteBufferedStringAsync(LengthFormat lengthEnc)
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

    public static IEnumerable<object[]> CharWriters()
    {
        yield return new object[] { new PoolingBufferWriter<char>(MemoryPool<char>.Shared.ToAllocator()) };
        yield return new object[] { new PoolingArrayBufferWriter<char>() };
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
            writer.Format(42, provider: InvariantCulture);
            writer.Format(56U, provider: InvariantCulture);
            writer.Format<byte>(10, provider: InvariantCulture);
            writer.Format<sbyte>(22, provider: InvariantCulture);
            writer.Format<short>(88, provider: InvariantCulture);
            writer.Format<ushort>(99, provider: InvariantCulture);
            writer.Format(77L, provider: InvariantCulture);
            writer.Format(66UL, provider: InvariantCulture);

            var guid = Guid.NewGuid();
            writer.Format(guid);

            var dt = DateTime.Now;
            writer.Format(dt, provider: InvariantCulture);

            var dto = DateTimeOffset.Now;
            writer.Format(dto, provider: InvariantCulture);

            writer.Format(42.5M, provider: InvariantCulture);
            writer.Format(32.2F, provider: InvariantCulture);
            writer.Format(56.6D, provider: InvariantCulture);

            Equal("Hello, world!!!" + Environment.NewLine + "4256102288997766" + guid + dt.ToString(InvariantCulture) + dto.ToString(InvariantCulture) + "42.532.256.6", writer.ToString());
        }
    }

    [Fact]
    public static void EncodeAsString()
    {
        using (var writer = new PoolingBufferWriter<byte>(MemoryPool<byte>.Shared.ToAllocator()))
        {
            EncodeDecode(writer, Encoding.UTF8);
        }

        using (var writer = new PoolingArrayBufferWriter<byte>())
        {
            EncodeDecode(writer, Encoding.UTF32);
        }

        using (var writer = new IO.FileBufferingWriter())
        {
            EncodeDecode(writer, Encoding.UTF8);
        }

        static void EncodeDecode<TBuffer>(TBuffer writer, Encoding encoding)
            where TBuffer : class, IBufferWriter<byte>, IDisposable, IGrowableBuffer<byte>
        {
            var encodingContext = new EncodingContext(encoding, true);
            var g = Guid.NewGuid();
            var dt = DateTime.Now;
            var dto = DateTimeOffset.Now;
            writer.Format(42L, in encodingContext, LengthFormat.LittleEndian, provider: InvariantCulture);
            writer.Format(12UL, in encodingContext, LengthFormat.LittleEndian, provider: InvariantCulture);
            writer.Format(34, in encodingContext, LengthFormat.BigEndian, provider: InvariantCulture);
            writer.Format(78U, in encodingContext, LengthFormat.LittleEndian, provider: InvariantCulture);
            writer.Format<short>(90, in encodingContext, LengthFormat.LittleEndian, provider: InvariantCulture);
            writer.Format<ushort>(12, in encodingContext, LengthFormat.LittleEndian, format: "X", provider: InvariantCulture);
            writer.Format<ushort>(12, in encodingContext, LengthFormat.LittleEndian, provider: InvariantCulture);
            writer.Format<byte>(10, in encodingContext, LengthFormat.LittleEndian, format: "X", provider: InvariantCulture);
            writer.Format<sbyte>(11, in encodingContext, LengthFormat.LittleEndian, format: "X", provider: InvariantCulture);
            writer.Format<byte>(10, in encodingContext, LengthFormat.LittleEndian, provider: InvariantCulture);
            writer.Format<sbyte>(11, in encodingContext, LengthFormat.LittleEndian, provider: InvariantCulture);
            writer.Format(g, in encodingContext, LengthFormat.LittleEndian);
            writer.Format(g, in encodingContext, LengthFormat.LittleEndian, format: "X");
            writer.Format(dt, in encodingContext, LengthFormat.LittleEndian, format: "O", provider: InvariantCulture);
            writer.Format(dto, in encodingContext, LengthFormat.LittleEndian, format: "O", provider: InvariantCulture);
            writer.Format(dt, in encodingContext, LengthFormat.LittleEndian, format: "O", provider: InvariantCulture);
            writer.Format(dto, in encodingContext, LengthFormat.LittleEndian, format: "O", provider: InvariantCulture);
            writer.Format(42.5M, in encodingContext, LengthFormat.LittleEndian, provider: InvariantCulture);
            writer.Format(32.2F, in encodingContext, LengthFormat.LittleEndian, provider: InvariantCulture);
            writer.Format(56.6D, in encodingContext, LengthFormat.LittleEndian, provider: InvariantCulture);

            var decodingContext = new DecodingContext(encoding, true);
            True(writer.TryGetWrittenContent(out var writtenMemory));
            var reader = IAsyncBinaryReader.Create(writtenMemory);
            Equal(42L, reader.Parse<IFormatProvider, long>(InvariantCulture, long.Parse, in decodingContext, LengthFormat.LittleEndian));
            Equal(12UL, reader.Parse<IFormatProvider, ulong>(InvariantCulture, ulong.Parse, in decodingContext, LengthFormat.LittleEndian));
            Equal(34, reader.Parse<IFormatProvider, int>(InvariantCulture, int.Parse, in decodingContext, LengthFormat.BigEndian));
            Equal(78U, reader.Parse<uint>(in decodingContext, LengthFormat.LittleEndian, NumberStyles.Integer, InvariantCulture));
            Equal(90, reader.Parse<short>(in decodingContext, LengthFormat.LittleEndian, NumberStyles.Integer, InvariantCulture));
            Equal(12, reader.Parse<ushort>(in decodingContext, LengthFormat.LittleEndian, NumberStyles.Integer, InvariantCulture));
            Equal(10, reader.Parse<byte>(in decodingContext, LengthFormat.LittleEndian, NumberStyles.Integer, InvariantCulture));
            Equal(11, reader.Parse<sbyte>(in decodingContext, LengthFormat.LittleEndian, NumberStyles.Integer, InvariantCulture));
            Equal(g, reader.Parse<IFormatProvider, Guid>(InvariantCulture, Guid.Parse, in decodingContext, LengthFormat.LittleEndian));
            Equal(g, reader.Parse<IFormatProvider, Guid>(InvariantCulture, static (c, p) => Guid.ParseExact(c, "X"), in decodingContext, LengthFormat.LittleEndian));
            Equal(dt, reader.Parse<IFormatProvider, DateTime>(InvariantCulture, static (c, p) => DateTime.Parse(c, p, DateTimeStyles.RoundtripKind), in decodingContext, LengthFormat.LittleEndian));
            Equal(dto, reader.Parse<IFormatProvider, DateTimeOffset>(InvariantCulture, static (c, p) => DateTimeOffset.Parse(c, p, DateTimeStyles.RoundtripKind), in decodingContext, LengthFormat.LittleEndian));
            Equal(dt, reader.Parse<IFormatProvider, DateTime>(InvariantCulture, static (c, p) => DateTime.ParseExact(c, "O", p, DateTimeStyles.RoundtripKind), in decodingContext, LengthFormat.LittleEndian));
            Equal(dto, reader.Parse<IFormatProvider, DateTimeOffset>(InvariantCulture, static (c, p) => DateTimeOffset.ParseExact(c, "O", p, DateTimeStyles.RoundtripKind), in decodingContext, LengthFormat.LittleEndian));
            Equal(42.5M, reader.Parse<decimal>(in decodingContext, LengthFormat.LittleEndian, NumberStyles.Float, InvariantCulture));
            Equal(32.2F, reader.Parse<float>(in decodingContext, LengthFormat.LittleEndian, NumberStyles.Float, InvariantCulture));
            Equal(56.6D, reader.Parse<double>(in decodingContext, LengthFormat.LittleEndian, NumberStyles.Float, InvariantCulture));
        }
    }

    [Fact]
    public static void FormatValues()
    {
        using var writer = new PoolingArrayBufferWriter<char> { Capacity = 64 };

        const string expectedString = "Hello, world!";
        Equal(expectedString.Length, writer.Format(expectedString));
        Equal(expectedString, writer.ToString());
        writer.Clear();

        Equal(2, writer.Format(56, provider: InvariantCulture));
        Equal("56", writer.ToString());
    }

    public static IEnumerable<object[]> ContiguousBuffers()
    {
        yield return new object[] { new PoolingBufferWriter<byte>() };
        yield return new object[] { new PoolingArrayBufferWriter<byte>() };
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
        using var buffer = new PoolingArrayBufferWriter<char>();

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

        using var buffer = new PoolingArrayBufferWriter<char>();
        buffer.Interpolate($"{await xt,4:X} = {await yt,-3:X}");
        Equal($"{x,4:X} = {y,-3:X}", buffer.ToString());
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(int.MaxValue, int.MinValue)]
    public static void WriteInterpolatedStringToBufferWriterSlim(int x, int y)
    {
        var buffer = new BufferWriterSlim<char>(stackalloc char[4]);
        buffer.Interpolate($"{x,4:X} = {y,-3:X}");
        Equal($"{x,4:X} = {y,-3:X}", buffer.ToString());
        buffer.Dispose();
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
        True(writer.Interpolate(in context, buffer, $"{x,4:X} = {y,-3:X}") > 0);

        Equal($"{x,4:X} = {y,-3:X}", context.Encoding.GetString(writer.WrittenSpan));
    }

    [Fact]
    public static void Concatenation()
    {
        var writer = new ArrayBufferWriter<char>();
        writer.Concat([]);
        Empty(writer.WrittenSpan.ToString());

        writer.Concat(["Hello, world!"]);
        Equal("Hello, world!", writer.WrittenSpan.ToString());
        writer.Clear();

        writer.Concat(["Hello, ", "world!"]);
        Equal("Hello, world!", writer.WrittenSpan.ToString());
    }

    [Fact]
    public static void ChangeWrittenCount()
    {
        using var buffer = new PoolingArrayBufferWriter<int>();

        Throws<ArgumentOutOfRangeException>(() => buffer.WrittenCount = 1);

        buffer.Add(42);
        Equal(1, buffer.WrittenCount);

        buffer.WrittenCount = 0;
        Equal(0, buffer.WrittenCount);

        buffer.WrittenCount = 1;
        Equal(42, buffer[0]);
    }

    [Fact]
    public static void AdvanceRewind()
    {
        var buffer = new PoolingArrayBufferWriter<int>();

        Throws<ArgumentOutOfRangeException>(() => buffer.Rewind(1));

        buffer.Add(42);
        Equal(1, buffer.WrittenCount);

        buffer.Rewind(1);
        Equal(0, buffer.WrittenCount);

        buffer.Advance(1);
        Equal(42, buffer[0]);
    }

    [Fact]
    public static void EncodeAsUtf8()
    {
        var writer = new ArrayBufferWriter<byte>();
        writer.Format(42);
        Equal(2, writer.WrittenCount);
        Equal(42, int.Parse(writer.WrittenSpan));
    }

    [Fact]
    public static void Rendering()
    {
        var writer = new ArrayBufferWriter<char>();
        writer.Format(CompositeFormat.Parse("{0}, {1}!"), ["Hello", "world"]);
        Equal("Hello, world!", writer.WrittenSpan.ToString());
    }
}
using System.Buffers;
using System.Globalization;
using System.Text;
using static System.Globalization.CultureInfo;

namespace DotNext.Buffers;

using IO;
using DecodingContext = DotNext.Text.DecodingContext;
using EncodingContext = DotNext.Text.EncodingContext;

public sealed class BufferWriterTests : Test
{
    [Fact]
    public static async Task ReadBlittableTypes()
    {
        var writer = new ArrayBufferWriter<byte>();
        writer.WriteLittleEndian(42L);
        writer.WriteLittleEndian(44);
        writer.WriteLittleEndian<short>(46);

        IAsyncBinaryReader reader = new SequenceReader(writer.WrittenMemory);
        Equal(42L, await reader.ReadLittleEndianAsync<long>(TestToken));
        Equal(44, await reader.ReadLittleEndianAsync<int>(TestToken));
        Equal(46, await reader.ReadLittleEndianAsync<short>(TestToken));
    }

    private static async Task ReadWriteStringUsingEncodingAsync(string value, Encoding encoding, LengthFormat lengthEnc)
    {
        var writer = new ArrayBufferWriter<byte>();
        writer.Encode(value.AsSpan(), encoding, lengthEnc);
        IAsyncBinaryReader reader = new SequenceReader(writer.WrittenMemory);
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

    public static TheoryData<IBufferWriter<char>> CharWriters() => new()
    {
        new PoolingBufferWriter<char>(MemoryPool<char>.Shared.ToAllocator()),
        new PoolingArrayBufferWriter<char>(),
        new SparseBufferWriter<char>(),
        new SparseBufferWriter<char>(32),
    };

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

        using (var writer = new FileBufferingWriter())
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
            var reader = new SequenceReader(writtenMemory);
            Equal(42L, reader.Parse<IFormatProvider, long>(InvariantCulture, long.Parse, in decodingContext, LengthFormat.LittleEndian));
            Equal(12UL, reader.Parse<IFormatProvider, ulong>(InvariantCulture, ulong.Parse, in decodingContext, LengthFormat.LittleEndian));
            Equal(34, reader.Parse<IFormatProvider, int>(InvariantCulture, int.Parse, in decodingContext, LengthFormat.BigEndian));
            Equal(78U, reader.Parse<uint>(in decodingContext, LengthFormat.LittleEndian, NumberStyles.Integer, InvariantCulture));
            Equal(90, reader.Parse<short>(in decodingContext, LengthFormat.LittleEndian, NumberStyles.Integer, InvariantCulture));
            Equal(12, reader.Parse<ushort>(in decodingContext, LengthFormat.LittleEndian, NumberStyles.HexNumber, InvariantCulture));
            Equal(12, reader.Parse<ushort>(in decodingContext, LengthFormat.LittleEndian, NumberStyles.Integer, InvariantCulture));
            Equal(10, reader.Parse<byte>(in decodingContext, LengthFormat.LittleEndian, NumberStyles.HexNumber, InvariantCulture));
            Equal(11, reader.Parse<sbyte>(in decodingContext, LengthFormat.LittleEndian, NumberStyles.HexNumber, InvariantCulture));
            Equal(10, reader.Parse<byte>(in decodingContext, LengthFormat.LittleEndian, NumberStyles.Integer, InvariantCulture));
            Equal(11, reader.Parse<sbyte>(in decodingContext, LengthFormat.LittleEndian, NumberStyles.Integer, InvariantCulture));
            Equal(g, reader.Parse<IFormatProvider, Guid>(InvariantCulture, Guid.Parse, in decodingContext, LengthFormat.LittleEndian));
            Equal(g, reader.Parse<IFormatProvider, Guid>(InvariantCulture, static (c, _) => Guid.ParseExact(c, "X"), in decodingContext, LengthFormat.LittleEndian));
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

    public static TheoryData<BufferWriter<byte>> ContiguousBuffers() =>
    [
        new PoolingBufferWriter<byte>(),
        new PoolingArrayBufferWriter<byte>()
    ];

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
            Equal(bytes, buffer.Memory);
            buffer.Dispose();
        }
    }

    public static TheoryData<BufferWriter<byte>> BoundedBuffers() =>
    [
        new PoolingBufferWriter<byte> { MaxCapacity = 16 },
        new PoolingArrayBufferWriter<byte> { MaxCapacity = 16 },
    ];

    [Theory]
    [MemberData(nameof(BoundedBuffers))]
    public static void GrowWithinMaxCapacity(BufferWriter<byte> writer)
    {
        using (writer)
        {
            Equal(16, writer.MaxCapacity);

            writer.Write(new byte[16]);
            Equal(16, writer.WrittenCount);
            True(writer.Capacity >= 16);
        }
    }

    [Theory]
    [MemberData(nameof(BoundedBuffers))]
    public static void GrowBeyondMaxCapacity(BufferWriter<byte> writer)
    {
        using (writer)
            Throws<InsufficientMemoryException>(() => writer.Write(new byte[17]));
    }

    [Fact]
    public static void GrowClampedToMaxCapacity()
    {
        // ArrayPool<byte>.Shared.Rent(16) returns an array of exactly 16 elements,
        // so the internal capacity is observable here.
        using var writer = new PoolingArrayBufferWriter<byte> { MaxCapacity = 16 };

        // Without a limit, the first write would grow the buffer to the default
        // initial size (128). Because the write exactly matches MaxCapacity, growth
        // is clamped to 16 instead, and the write still succeeds.
        writer.Write(new byte[16]);
        Equal(16, writer.WrittenCount);
        Equal(16, writer.Capacity);
        Equal(0, writer.FreeCapacity);

        // The buffer sits exactly at MaxCapacity, so any further growth must throw.
        Throws<InsufficientMemoryException>(() => writer.Add(0));
    }

    [Fact]
    public static void MaxCapacityValidation()
    {
        Throws<ArgumentOutOfRangeException>(static () => new PoolingBufferWriter<byte> { MaxCapacity = 0 });
        Throws<ArgumentOutOfRangeException>(static () => new PoolingArrayBufferWriter<byte> { MaxCapacity = -1 });
    }

    [Fact]
    public static void UnboundedMaxCapacityByDefault()
    {
        using var writer1 = new PoolingBufferWriter<byte>();
        Equal(int.MaxValue, writer1.MaxCapacity);

        using var writer2 = new PoolingArrayBufferWriter<byte>();
        Equal(int.MaxValue, writer2.MaxCapacity);
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
        using var buffer = new PoolingArrayBufferWriter<int>();

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
    
    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(128)]
    [InlineData(124)]
    public static void WriteStringBuilder(int stringLength)
    {
        var str = Random.Shared.GetString(Alphabet, stringLength);

        var builder = new StringBuilder();
        for (var i = 0; i < 3; i++)
        {
            builder.Append(str);
        }

        var writer = new BufferWriterSlim<char>();

        writer.Write(builder);
        Equal(builder.ToString(), writer.WrittenSpan);
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(128)]
    [InlineData(124)]
    public static void WriteStringBuilder2(int stringLength)
    {
        var str = Random.Shared.GetString(Alphabet, stringLength);

        var builder = new StringBuilder();
        for (var i = 0; i < 3; i++)
        {
            builder.Append(str);
        }

        var writer = new ArrayBufferWriter<char>();

        writer.Write(builder);
        Equal(builder.ToString(), writer.WrittenSpan);
    }

    [Theory]
    [MemberData(nameof(ContiguousBuffers))]
    public static void WriteLargeBuffer(BufferWriter<byte> writer)
    {
        var expectedData = RandomBytes(1024 * 1024);
        writer.Write(expectedData);
        
        Equal(expectedData, writer.WrittenMemory.Span);
    }
}
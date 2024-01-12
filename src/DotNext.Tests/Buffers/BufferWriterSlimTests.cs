using System.Text;
using static System.Globalization.CultureInfo;

namespace DotNext.Buffers;

public sealed class BufferWriterSlimTests : Test
{
    [Fact]
    public static void GrowableBuffer()
    {
        using var builder = new BufferWriterSlim<int>(stackalloc int[2]);
        Equal(0, builder.WrittenCount);
        Equal(2, builder.Capacity);
        Equal(2, builder.FreeCapacity);

        builder.Write(new int[] { 10, 20 });
        Equal(2, builder.WrittenCount);
        Equal(2, builder.Capacity);
        Equal(0, builder.FreeCapacity);

        Equal(10, builder[0]);
        Equal(20, builder[1]);

        builder.Write([30, 40]);
        Equal(4, builder.WrittenCount);
        True(builder.Capacity >= 2);
        Equal(30, builder[2]);
        Equal(40, builder[3]);
        Span<int> result = stackalloc int[5];
        builder.WrittenSpan.CopyTo(result, out var writtenCount);
        Equal(4, writtenCount);
        Equal([10, 20, 30, 40, 0], result);

        builder.Clear(true);
        Equal(0, builder.WrittenCount);
        builder.Write([50, 60, 70, 80]);
        Equal(4, builder.WrittenCount);
        True(builder.Capacity >= 2);
        Equal(50, builder[0]);
        Equal(60, builder[1]);
        Equal(70, builder[2]);
        Equal(80, builder[3]);

        builder.Clear(false);
        Equal(0, builder.WrittenCount);
        builder.Write([10, 20, 30, 40]);
        Equal(4, builder.WrittenCount);
        True(builder.Capacity >= 2);
        Equal(10, builder[0]);
        Equal(20, builder[1]);
        Equal(30, builder[2]);
        Equal(40, builder[3]);
    }

    [Fact]
    public static void EmptyBuilder()
    {
        using var builder = new BufferWriterSlim<int>();
        Equal(0, builder.Capacity);
        builder.Add() = 10;
        Equal(1, builder.WrittenCount);
        Equal(10, builder[0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    [InlineData(64)]
    public static void MutableOnStackWriter(int initialBufferSize)
    {
        var writer = new BufferWriterSlim<char>(initialBufferSize > 0 ? stackalloc char[initialBufferSize] : Span<char>.Empty);
        try
        {
            writer.Write("Hello, world");
            writer.Add('!');
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
            writer.Format(guid, provider: InvariantCulture);

            var dt = DateTime.Now;
            writer.Format(dt, provider: InvariantCulture);

            var dto = DateTimeOffset.Now;
            writer.Format(dto, provider: InvariantCulture);

            writer.Format(42.5M, provider: InvariantCulture);
            writer.Format(32.2F, provider: InvariantCulture);
            writer.Format(56.6D, provider: InvariantCulture);

            Equal("Hello, world!!!" + Environment.NewLine + "4256102288997766" + guid + dt.ToString(InvariantCulture) + dto.ToString(InvariantCulture) + "42.532.256.6", writer.ToString());
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Fact]
    public static void ReadWritePrimitives()
    {
        var builder = new BufferWriterSlim<byte>(stackalloc byte[512]);
        try
        {
            builder.WriteLittleEndian(short.MinValue);
            builder.WriteBigEndian(short.MaxValue);
            builder.WriteLittleEndian<ushort>(42);
            builder.WriteBigEndian(ushort.MaxValue);
            builder.WriteLittleEndian(int.MaxValue);
            builder.WriteBigEndian(int.MinValue);
            builder.WriteLittleEndian(42U);
            builder.WriteBigEndian(uint.MaxValue);
            builder.WriteLittleEndian(long.MaxValue);
            builder.WriteBigEndian(long.MinValue);
            builder.WriteLittleEndian(42UL);
            builder.WriteBigEndian(ulong.MaxValue);

            var reader = new SpanReader<byte>(builder.WrittenSpan);
            Equal(short.MinValue, reader.ReadLittleEndian<short>());
            Equal(short.MaxValue, reader.ReadBigEndian<short>());
            Equal(42, reader.ReadLittleEndian<ushort>());
            Equal(ushort.MaxValue, reader.ReadBigEndian<ushort>());
            Equal(int.MaxValue, reader.ReadLittleEndian<int>());
            Equal(int.MinValue, reader.ReadBigEndian<int>());
            Equal(42U, reader.ReadLittleEndian<uint>());
            Equal(uint.MaxValue, reader.ReadBigEndian<uint>());
            Equal(long.MaxValue, reader.ReadLittleEndian<long>());
            Equal(long.MinValue, reader.ReadBigEndian<long>());
            Equal(42UL, reader.ReadLittleEndian<ulong>());
            Equal(ulong.MaxValue, reader.ReadBigEndian<ulong>());
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Fact]
    public static void EscapeBuffer()
    {
        using var buffer = new BufferWriterSlim<int>(stackalloc int[2]);
        buffer.Add(10);
        buffer.Add(20);
        False(buffer.TryDetachBuffer(out var owner));

        buffer.Add(30);
        True(buffer.TryDetachBuffer(out owner));
        Equal(0, buffer.WrittenCount);
        Equal(10, owner[0]);
        Equal(20, owner[1]);
        Equal(30, owner[2]);
        Equal(3, owner.Length);
        owner.Dispose();
    }

    [Fact]
    public static void FormatValues()
    {
        var writer = new BufferWriterSlim<char>(stackalloc char[64]);
        try
        {
            const string expectedString = "Hello, world!";
            Equal(expectedString.Length, writer.Format(expectedString));
            Equal(expectedString, writer.ToString());
            writer.Clear();

            Equal(2, writer.Format(56));
            Equal("56", writer.ToString());
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Fact]
    public static void Concatenation()
    {
        var writer = new BufferWriterSlim<char>(stackalloc char[32]);
        try
        {
            writer.Concat([]);
            Empty(writer.ToString());

            writer.Concat(["Hello, world!"]);
            Equal("Hello, world!", writer.ToString());
            writer.Clear(reuseBuffer: true);

            writer.Concat(["Hello, ", "world!"]);
            Equal("Hello, world!", writer.ToString());
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Fact]
    public static void StackBehavior()
    {
        var writer = new BufferWriterSlim<int>(stackalloc int[4]);
        False(writer.TryPeek(out var item));
        False(writer.TryPop(out item));

        writer.Add(42);
        True(writer.TryPeek(out item));
        Equal(42, item);
        True(writer.TryPeek(out item));
        Equal(42, item);

        True(writer.TryPop(out item));
        Equal(42, item);
        False(writer.TryPop(out item));

        writer.Dispose();
    }

    [Fact]
    public static void RemoveMultipleElements()
    {
        Span<int> buffer = stackalloc int[4];
        var writer = new BufferWriterSlim<int>(stackalloc int[4]);

        False(writer.TryPop(buffer));
        True(writer.TryPop(Span<int>.Empty));

        writer.Write([10, 20, 30]);
        True(writer.TryPop(buffer.Slice(0, 2)));
        Equal(20, buffer[0]);
        Equal(30, buffer[1]);
        False(writer.TryPop(buffer));

        True(writer.TryPop(buffer.Slice(0, 1)));
        Equal(10, buffer[0]);
        Equal(0, writer.WrittenCount);
    }

    [Fact]
    public static void AdvanceRewind()
    {
        var buffer = new BufferWriterSlim<int>(stackalloc int[3]);

        var raised = false;
        try
        {
            buffer.Rewind(1);
        }
        catch (ArgumentOutOfRangeException)
        {
            raised = true;
        }

        True(raised);

        buffer.Add(42);
        Equal(1, buffer.WrittenCount);

        buffer.Rewind(1);
        Equal(0, buffer.WrittenCount);

        buffer.Advance(1);
        Equal(42, buffer[0]);
    }

    [Fact]
    public static void ChangeWrittenCount()
    {
        var buffer = new BufferWriterSlim<int>(stackalloc int[3]);

        var raised = false;
        try
        {
            buffer.WrittenCount = 4;
        }
        catch (ArgumentOutOfRangeException)
        {
            raised = true;
        }

        True(raised);

        buffer.Add(42);
        Equal(1, buffer.WrittenCount);

        buffer.WrittenCount = 0;
        Equal(0, buffer.WrittenCount);

        buffer.WrittenCount = 1;
        Equal(42, buffer[0]);
    }

    [Fact]
    public static void EncodeAsUtf8()
    {
        var writer = new BufferWriterSlim<byte>();
        writer.Format(42);
        Equal(2, writer.WrittenCount);
        Equal(42, int.Parse(writer.WrittenSpan));
    }

    [Fact]
    public static void Rendering()
    {
        var writer = new BufferWriterSlim<char>();
        writer.Format(CompositeFormat.Parse("{0}, {1}!"), ["Hello", "world"]);
        Equal("Hello, world!", writer.ToString());
    }
}
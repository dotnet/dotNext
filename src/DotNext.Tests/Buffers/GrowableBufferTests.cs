using System.Diagnostics.CodeAnalysis;

namespace DotNext.Buffers;

using IO;

[ExcludeFromCodeCoverage]
public sealed class GrowableBufferTests : Test
{
    private sealed class ArrayCopyOperation
    {
        private readonly byte[] output;
        private int offset;

        internal ArrayCopyOperation(byte[] output)
        {
            this.output = output;
            offset = 0;
        }

        private void Append(ReadOnlySpan<byte> input)
        {
            input.CopyTo(output.AsSpan(offset), out var writtenCount);
            offset += writtenCount;
        }

        internal static void Append(ReadOnlySpan<byte> input, ArrayCopyOperation output)
            => output.Append(input);
    }

    private static unsafe void ReadWriteTest(IGrowableBuffer<byte> writer)
    {
        // write a few bytes
        writer.Write(255);
        writer.Write(0);
        Equal(2L, writer.WrittenCount);
        var actual = new byte[2];
        Equal(2, writer.CopyTo(actual));
        Equal(new byte[] { 255, 0 }, actual);

        writer.Clear();
        var expected = RandomBytes(5000);
        writer.Write(expected);
        actual = new byte[expected.Length];
        writer.CopyTo<ReadOnlySpanConsumer<byte, ArrayCopyOperation>>(new ReadOnlySpanConsumer<byte, ArrayCopyOperation>(&ArrayCopyOperation.Append, new ArrayCopyOperation(actual)));
        Equal(expected, actual);
    }

    [Fact]
    public static void ReadWriteUsingPooledBufferWriter()
    {
        using var writer = new PooledBufferWriter<byte>();
        Null(writer.As<IGrowableBuffer<byte>>().Capacity);
        ReadWriteTest(writer);
    }

    [Fact]
    public static void ReadWriteUsingPooledArrayBufferWriter()
    {
        using var writer = new PooledArrayBufferWriter<byte>();
        Null(writer.As<IGrowableBuffer<byte>>().Capacity);
        ReadWriteTest(writer);
    }

    [Fact]
    public static void ReadWriteUsingSequenceBuilder()
    {
        using var builder = new SequenceBuilder<byte>();
        Null(builder.As<IGrowableBuffer<byte>>().Capacity);
        ReadWriteTest(builder);
    }

    [Fact]
    public static void ReadWriteUsingFileBufferingWriter()
    {
        using var writer = new FileBufferingWriter(asyncIO: false);
        Null(writer.As<IGrowableBuffer<byte>>().Capacity);
        ReadWriteTest(writer);
    }
}
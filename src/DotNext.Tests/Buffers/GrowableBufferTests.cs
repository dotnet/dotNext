namespace DotNext.Buffers;

using IO;
using Runtime;

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
            offset += input >> output.AsSpan(offset);
        }

        internal static void Append(ReadOnlySpan<byte> input, ArrayCopyOperation output)
            => output.Append(input);
    }

    private static unsafe void ReadWriteTest<TBuffer>(Func<TBuffer> factory)
        where TBuffer : IGrowableBuffer<byte>, allows ref struct
    {
        var writer = factory();
        try
        {
            // write a few bytes
            writer.Write(255);
            writer.Write(0);
            Equal(2L, writer.WrittenCount);
            var actual = new byte[2];
            Equal(2, writer.CopyTo(actual));
            Equal(new byte[] { 255, 0 }, actual);

            writer.Reset();
            var expected = RandomBytes(5000);
            writer.Write(expected);
            actual = new byte[expected.Length];
            writer.CopyTo<ReadOnlySpanConsumer<byte, ArrayCopyOperation>>(new(&ArrayCopyOperation.Append, new ArrayCopyOperation(actual)));
            Equal(expected, actual);
            
            writer.Reset();
            var expectedSpan = new ReadOnlySpan<byte>(expected);
            var arg = Variant.Immutable(ref expectedSpan);
#pragma warning disable CS9080
            writer.DynamicInvoke(in arg, 1, Variant.Empty);
#pragma warning restore CS9080
            Equal(expectedSpan.Length, writer.WrittenCount);
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Fact]
    public static void ReadWriteUsingPooledBufferWriter()
    {
        ReadWriteTest(static () => new PoolingBufferWriter<byte>());
    }

    [Fact]
    public static void ReadWriteUsingPooledArrayBufferWriter()
    {
        ReadWriteTest(static () => new PoolingArrayBufferWriter<byte> { Capacity = 8 });
    }

    [Fact]
    public static void ReadWriteUsingSequenceBuilder()
    {
        ReadWriteTest(static () => new SequenceBuilder<byte>());
    }

    [Fact]
    public static void ReadWriteUsingFileBufferingWriter()
    {
        ReadWriteTest(static () => new FileBufferingWriter(asyncIO: false));
    }

    [Fact]
    public static void ReadWriteUsingBufferWriter()
    {
        ReadWriteTest(static () => new BufferWriterSlim<byte>());
    }
}
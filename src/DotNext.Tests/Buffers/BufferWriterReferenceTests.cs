using System.Buffers;

namespace DotNext.Buffers;

public sealed class BufferWriterReferenceTests : Test
{
    [Fact]
    public static void BasicMethods()
    {
        var consumer = new BufferWriterReference<byte>();
        Null(consumer.ToString());
        True(consumer.IsEmpty);
        Equal(new BufferWriterReference<byte>(), consumer);
        True(new BufferWriterReference<byte>() == consumer);
        False(new BufferWriterReference<byte>() != consumer);
        Equal(0, consumer.GetHashCode());

        var writer = new ArrayBufferWriter<byte>();
        consumer = new BufferWriterReference<byte>(writer);
        NotNull(consumer.ToString());
        False(consumer.IsEmpty);
        NotEqual(0, consumer.GetHashCode());
        NotEqual(new BufferWriterReference<byte>(), consumer);
        False(new BufferWriterReference<byte>() == consumer);
        True(new BufferWriterReference<byte>() != consumer);
    }

    [Fact]
    public static void WriteMethods()
    {
        var ms = new ArrayBufferWriter<char>();
        var consumer = new BufferWriterReference<char>(ms);
        consumer.As<IConsumer<ReadOnlySpan<char>>>().Invoke(['1', '2']);
        consumer.As<ISupplier<ReadOnlyMemory<char>, CancellationToken, ValueTask>>().Invoke(new [] { '3', '4' }, TestToken);
        Equal("1234", ms.WrittenSpan.ToString());
    }
}
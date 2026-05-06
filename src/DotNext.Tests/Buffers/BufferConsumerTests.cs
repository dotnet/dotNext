using System.Buffers;

namespace DotNext.Buffers;

public sealed class BufferConsumerTests : Test
{
    [Fact]
    public static void BasicMethods()
    {
        var consumer = new BufferConsumer<byte>();
        Null(consumer.ToString());
        True(consumer.IsEmpty);
        Equal(new BufferConsumer<byte>(), consumer);
        True(new BufferConsumer<byte>() == consumer);
        False(new BufferConsumer<byte>() != consumer);
        Equal(0, consumer.GetHashCode());

        var writer = new ArrayBufferWriter<byte>();
        consumer = new BufferConsumer<byte>(writer);
        NotNull(consumer.ToString());
        False(consumer.IsEmpty);
        NotEqual(0, consumer.GetHashCode());
        NotEqual(new BufferConsumer<byte>(), consumer);
        False(new BufferConsumer<byte>() == consumer);
        True(new BufferConsumer<byte>() != consumer);
    }

    [Fact]
    public static void WriteMethods()
    {
        var ms = new ArrayBufferWriter<char>();
        var consumer = new BufferConsumer<char>(ms);
        consumer.As<IConsumer<ReadOnlySpan<char>>>().Invoke(['1', '2']);
        consumer.As<ISupplier<ReadOnlyMemory<char>, CancellationToken, ValueTask>>().Invoke(new [] { '3', '4' }, TestToken);
        Equal("1234", ms.WrittenSpan.ToString());
    }
}
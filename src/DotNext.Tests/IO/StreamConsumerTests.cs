namespace DotNext.IO;

using Buffers;

public sealed class StreamConsumerTests : Test
{
    [Fact]
    public static void BasicMethods()
    {
        Throws<ArgumentNullException>(() => new StreamConsumer(null));
        var consumer = new StreamConsumer();
        Null(consumer.ToString());
        True(consumer.IsEmpty);
        Equal(new StreamConsumer(), consumer);
        True(new StreamConsumer() == consumer);
        False(new StreamConsumer() != consumer);
        Equal(0, consumer.GetHashCode());

        consumer = Stream.Null;
        NotNull(consumer.ToString());
        False(consumer.IsEmpty);
        NotEqual(0, consumer.GetHashCode());
        NotEqual(new StreamConsumer(), consumer);
        False(new StreamConsumer() == consumer);
        True(new StreamConsumer() != consumer);
    }

    [Fact]
    public static void WriteMethods()
    {
        using var ms = new MemoryStream();
        IReadOnlySpanConsumer<byte> consumer = new StreamConsumer(ms);
        consumer.Invoke(new ReadOnlySpan<byte>([1, 2]));
        consumer.Invoke(new ReadOnlyMemory<byte>([3, 4]), TestToken);
        Equal(new byte[] { 1, 2, 3, 4 }, ms.ToArray());
    }
}
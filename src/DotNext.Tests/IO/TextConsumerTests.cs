namespace DotNext.IO;

using Buffers;

public sealed class TextConsumerTests : Test
{
    [Fact]
    public static void BasicMethods()
    {
        Throws<ArgumentNullException>(() => new TextConsumer(null));
        var consumer = new TextConsumer();
        Null(consumer.ToString());
        True(consumer.IsEmpty);
        Equal(new TextConsumer(), consumer);
        True(new TextConsumer() == consumer);
        False(new TextConsumer() != consumer);
        Equal(0, consumer.GetHashCode());

        consumer = TextWriter.Null;
        NotNull(consumer.ToString());
        False(consumer.IsEmpty);
        NotEqual(0, consumer.GetHashCode());
        NotEqual(new TextConsumer(), consumer);
        False(new TextConsumer() == consumer);
        True(new TextConsumer() != consumer);
    }

    [Fact]
    public static void WriteMethods()
    {
        using var ms = new StringWriter();
        IReadOnlySpanConsumer<char> consumer = new TextConsumer(ms);
        consumer.Invoke(new ReadOnlySpan<char>(new char[] { '1', '2' }));
        consumer.Invoke(new ReadOnlyMemory<char>(new char[] { '3', '4' }), default);
        Equal("1234", ms.ToString());
    }
}
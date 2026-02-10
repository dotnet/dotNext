namespace DotNext.IO;

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
        var consumer = new TextConsumer(ms);
        consumer.As<IConsumer<ReadOnlySpan<char>>>().Invoke(['1', '2']);
        consumer.As<ISupplier<ReadOnlyMemory<char>, CancellationToken, ValueTask>>().Invoke(new[] { '3', '4' }, TestToken);
        Equal("1234", ms.ToString());
    }
}
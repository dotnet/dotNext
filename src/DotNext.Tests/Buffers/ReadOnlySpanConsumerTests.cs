using System.Reflection;

namespace DotNext.Buffers;

public sealed class ReadOnlySpanConsumerTests : Test
{
    [Fact]
    public static void BasicMethods()
    {
        var consumer = new ReadOnlySpanConsumer<char, Missing>();
        True(consumer.IsEmpty);

        consumer = new ReadOnlySpanConsumer<char, Missing>(Consume, Missing.Value);
        False(consumer.IsEmpty);
        consumer.As<IConsumer<ReadOnlySpan<char>>>().Invoke(ReadOnlySpan<char>.Empty);
        consumer.As<ISupplier<ReadOnlyMemory<char>, CancellationToken, ValueTask>>().Invoke(ReadOnlyMemory<char>.Empty, TestToken);

        static void Consume(ReadOnlySpan<char> span, Missing value)
        {
            NotNull(value);
        }
    }
}
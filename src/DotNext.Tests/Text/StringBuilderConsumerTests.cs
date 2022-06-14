using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace DotNext.Text
{
    using Buffers;

    [ExcludeFromCodeCoverage]
    public sealed class StringBuilderConsumerTests : Test
    {
        [Fact]
        public static void BasicMethods()
        {
            Throws<ArgumentNullException>(() => new StringBuilderConsumer(null));
            var consumer = new StringBuilderConsumer();
            Null(consumer.ToString());
            True(consumer.IsEmpty);
            Equal(new StringBuilderConsumer(), consumer);
            True(new StringBuilderConsumer() == consumer);
            False(new StringBuilderConsumer() != consumer);
            Equal(0, consumer.GetHashCode());

            consumer = new StringBuilder();
            NotNull(consumer.ToString());
            False(consumer.IsEmpty);
            NotEqual(0, consumer.GetHashCode());
            NotEqual(new StringBuilderConsumer(), consumer);
            False(new StringBuilderConsumer() == consumer);
            True(new StringBuilderConsumer() != consumer);
        }

        [Fact]
        public static void WriteMethods()
        {
            var sb = new StringBuilder();
            IReadOnlySpanConsumer<char> consumer = new StringBuilderConsumer(sb);
            consumer.Invoke("Hello, ".AsSpan());
            consumer.Invoke("world!".AsMemory(), default);
            Equal("Hello, world!", consumer.ToString());
        }
    }
}
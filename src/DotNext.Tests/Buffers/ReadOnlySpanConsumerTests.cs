using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace DotNext.Buffers
{
    [ExcludeFromCodeCoverage]
    public sealed class ReadOnlySpanConsumerTests : Test
    {
        [Fact]
        public static void BufferConsumerBasicMethods()
        {
            Throws<ArgumentNullException>(() => new BufferConsumer<byte>(null));
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
        public static void BufferConsumerWriteMethods()
        {
            var ms = new ArrayBufferWriter<char>();
            IReadOnlySpanConsumer<char> consumer = new BufferConsumer<char>(ms);
            consumer.Invoke(new ReadOnlySpan<char>(new char[] { '1', '2' }));
            consumer.Invoke(new ReadOnlyMemory<char>(new char[] { '3', '4' }), default);
            Equal("1234", ms.WrittenSpan.ToString());
        }

        [Fact]
        public static unsafe void ReadOnlySpanConsumerBasicMethods()
        {
            Throws<ArgumentNullException>(() => new ReadOnlySpanConsumer<char, Missing>(null, Missing.Value));
            var consumer = new ReadOnlySpanConsumer<char, Missing>();
            True(consumer.IsEmpty);

            consumer = new ReadOnlySpanConsumer<char, Missing>(&Consume, Missing.Value);
            False(consumer.IsEmpty);
            ((IReadOnlySpanConsumer<char>)consumer).Invoke(ReadOnlySpan<char>.Empty);

            static void Consume(ReadOnlySpan<char> span, Missing value)
            {
                NotNull(value);
            }
        }

        [Fact]
        public static unsafe void DelegatingReadOnlySpanConsumerBasicMethods()
        {
            Throws<ArgumentNullException>(() => new DelegatingReadOnlySpanConsumer<char, Missing>(null, Missing.Value));
            var consumer = new DelegatingReadOnlySpanConsumer<char, Missing>();
            True(consumer.IsEmpty);

            consumer = new DelegatingReadOnlySpanConsumer<char, Missing>(Consume, Missing.Value);
            False(consumer.IsEmpty);
            ((IReadOnlySpanConsumer<char>)consumer).Invoke(ReadOnlySpan<char>.Empty);

            static void Consume(ReadOnlySpan<char> span, Missing value)
            {
                NotNull(value);
            }
        }
    }
}
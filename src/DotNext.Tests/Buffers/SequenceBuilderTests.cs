using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Buffers
{
    [ExcludeFromCodeCoverage]
    public sealed class SequenceBuilderTests : Test
    {
        [Fact]
        public static void UseAutoChunkSize()
        {
            using var builder = new SequenceBuilder<byte>();
            builder.Write(new byte[] { 10, 20, 30 });
            Equal(3L, builder.WrittenCount);
        }

        [Fact]
        public static void CheckFragmentation()
        {
            using var builder = new SequenceBuilder<byte>(1024);

            // must be greater than 4096 because it's a default size of arrays obtained from ArrayPool<T>.Shared
            var expected = RandomBytes(5000);
            builder.Write(expected);
            Equal(expected.Length, builder.WrittenCount);
            var actual = new byte[expected.Length];
            Equal(actual.Length, builder.CopyTo(actual));
            Equal(expected, actual);
        }

        [Fact]
        public static void BuildSequence()
        {
            using var builder = new SequenceBuilder<byte>(1024);

            // must be greater than 4096 because it's a default size of arrays obtained from ArrayPool<T>.Shared
            var expected = RandomBytes(5000);
            builder.Write(expected);
            Equal(expected, builder.As<IReadOnlySequenceSource<byte>>().Sequence.ToArray());
        }
    }
}
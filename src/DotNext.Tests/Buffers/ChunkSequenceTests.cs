using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Buffers
{
    [ExcludeFromCodeCoverage]
    public sealed class ChunkSequenceTests : Test
    {
        [Fact]
        public static void Concatenation()
        {
            ReadOnlyMemory<byte> block1 = default, block2 = default;
            Equal(Array.Empty<byte>(), block1.Concat(block2).ToArray());

            block1 = new byte[] { 1, 2 };
            Equal(new byte[] { 1, 2 }, block1.Concat(block2).ToArray());

            block2 = block1;
            block1 = default;
            Equal(new byte[] { 1, 2 }, block1.Concat(block2).ToArray());

            block1 = new byte[] { 3, 4 };
            Equal(new byte[] { 1, 2, 3, 4 }, block2.Concat(block1).ToArray());
        }

        [Fact]
        public static void Concatenation2()
        {
            ReadOnlyMemory<byte> block1 = default, block2 = default;
            Equal(Array.Empty<byte>(), new[] { block1, block2 }.ToReadOnlySequence().ToArray());

            block1 = new byte[] { 1, 2 };
            Equal(new byte[] { 1, 2 }, new[] { block1, block2 }.ToReadOnlySequence().ToArray());

            block2 = block1;
            block1 = default;
            Equal(new byte[] { 1, 2 }, new[] { block1, block2 }.ToReadOnlySequence().ToArray());

            block1 = new byte[] { 3, 4 };
            Equal(new byte[] { 1, 2, 3, 4 }, new[] { block2, block1 }.ToReadOnlySequence().ToArray());
        }

        [Fact]
        public static void CopyFromSequence()
        {
            var sequence = new ReadOnlySequence<byte>(new byte[] { 1, 2, 3 }.AsMemory());
            Span<byte> dest = new byte[3];
            sequence.CopyTo(dest, out var writtenCount);
            Equal(3, writtenCount);
            Equal(sequence.ToArray(), dest.ToArray());

            sequence = ToReadOnlySequence<byte>(RandomBytes(64), 16);
            dest = new byte[64];
            sequence.CopyTo(dest, out writtenCount);
            Equal(64, writtenCount);
            Equal(sequence.ToArray(), dest.ToArray());

            dest = new byte[100];
            sequence.CopyTo(dest, out writtenCount);
            Equal(64, writtenCount);
            Equal(sequence.ToArray(), dest.Slice(0, 64).ToArray());

            dest = new byte[10];
            sequence.CopyTo(dest, out writtenCount);
            Equal(10, writtenCount);
            Equal(sequence.Slice(0, 10).ToArray(), dest.ToArray());
        }
    }
}

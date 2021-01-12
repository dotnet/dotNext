using System;
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

            block1 = new byte[] {1, 2};
            Equal(new byte[] {1, 2}, block1.Concat(block2).ToArray());

            block2 = block1;
            block1 = default;
            Equal(new byte[] {1, 2}, block1.Concat(block2).ToArray());

            block1 = new byte[] {3, 4};
            Equal(new byte[] {1, 2, 3, 4}, block2.Concat(block1).ToArray());
        }

        [Fact]
        public static void Concatenation2()
        {
            ReadOnlyMemory<byte> block1 = default, block2 = default;
            Equal(Array.Empty<byte>(), new[] {block1, block2}.ToReadOnlySequence().ToArray());

            block1 = new byte[] {1, 2};
            Equal(new byte[] {1, 2}, new[] {block1, block2}.ToReadOnlySequence().ToArray());

            block2 = block1;
            block1 = default;
            Equal(new byte[] {1, 2}, new[] {block1, block2}.ToReadOnlySequence().ToArray());

            block1 = new byte[] {3, 4};
            Equal(new byte[] {1, 2, 3, 4}, new[] {block2, block1}.ToReadOnlySequence().ToArray());
        }
    }
}

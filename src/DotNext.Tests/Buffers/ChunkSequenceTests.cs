using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Buffers;

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
        Equal(new byte[] { 1, 2 }, new List<ReadOnlyMemory<byte>> { block1, block2 }.ToReadOnlySequence().ToArray());

        block2 = block1;
        block1 = default;
        Equal(new byte[] { 1, 2 }, ToEnumerable(block1, block2).ToReadOnlySequence().ToArray());

        block1 = new byte[] { 3, 4 };
        Equal(new byte[] { 1, 2, 3, 4 }, new[] { block2, block1 }.ToReadOnlySequence().ToArray());

        static IEnumerable<ReadOnlyMemory<byte>> ToEnumerable(ReadOnlyMemory<byte> block1, ReadOnlyMemory<byte> block2)
        {
            yield return block1;
            yield return block2;
        }
    }

    [Fact]
    public static void StringConcatenation()
    {
        string block1 = string.Empty, block2 = null;
        Equal(string.Empty, new[] { block1, block2 }.ToReadOnlySequence().ToString());

        block1 = "Hello";
        Equal(block1, new List<string> { block1, block2 }.ToReadOnlySequence().ToString());

        block2 = block1;
        block1 = default;
        Equal(block2, ToEnumerable(block1, block2).ToReadOnlySequence().ToString());

        block1 = "Hello, ";
        block2 = "world!";
        Equal(block1 + block2, new[] { block1, block2 }.ToReadOnlySequence().ToString());

        static IEnumerable<string> ToEnumerable(string block1, string block2)
        {
            yield return block1;
            yield return block2;
        }
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
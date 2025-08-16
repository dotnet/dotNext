using System.Buffers;
using System.Text;

namespace DotNext.Buffers;

public sealed class ChunkSequenceTests : Test
{
    [Fact]
    public static void Concatenation()
    {
        ReadOnlyMemory<byte> block1 = default, block2 = default;
        Equal([], block1.Concat(block2).ToArray());

        block1 = new byte[] { 1, 2 };
        Equal([1, 2], block1.Concat(block2).ToArray());

        block2 = block1;
        block1 = default;
        Equal([1, 2], block1.Concat(block2).ToArray());

        block1 = new byte[] { 3, 4 };
        Equal([1, 2, 3, 4], block2.Concat(block1).ToArray());
    }

    [Fact]
    public static void Concatenation2()
    {
        ReadOnlyMemory<byte> block1 = default, block2 = default;
        Equal([], new[] { block1, block2 }.ToReadOnlySequence().ToArray());

        block1 = new byte[] { 1, 2 };
        Equal([1, 2], new List<ReadOnlyMemory<byte>> { block1, block2 }.ToReadOnlySequence().ToArray());

        block2 = block1;
        block1 = default;
        Equal([1, 2], ToEnumerable(block1, block2).ToReadOnlySequence().ToArray());

        block1 = new byte[] { 3, 4 };
        Equal([1, 2, 3, 4], new[] { block2, block1 }.ToReadOnlySequence().ToArray());

        static IEnumerable<ReadOnlyMemory<byte>> ToEnumerable(ReadOnlyMemory<byte> block1, ReadOnlyMemory<byte> block2)
        {
            yield return block1;
            yield return block2;
        }
    }

    [Fact]
    public static void Concatenation3()
    {
        Equal([], Memory.ToReadOnlySequence([]).ToArray());
        
        ReadOnlyMemory<byte> block1 = new byte[] { 1, 2 };
        Equal(block1.Span, Memory.ToReadOnlySequence([block1]).ToArray());
        
        ReadOnlyMemory<byte> block2 = new byte[] { 3, 4 };
        Equal([1, 2, 3, 4], Memory.ToReadOnlySequence([block1, block2]).ToArray());
    }

    [Fact]
    public static void StringConcatenation1()
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
    public static void StringConcatenation2()
    {
        Equal([], Memory.ToReadOnlySequence(ReadOnlySpan<string>.Empty).ToArray());
        
        const string block1 = "Hello";
        Equal(block1, Memory.ToReadOnlySequence([block1]).ToString());

        const string block2 = ", world!";
        Equal(block1 + block2, Memory.ToReadOnlySequence([block1, block2]).ToString());
    }

    [Fact]
    public static void CopyFromSequence()
    {
        var sequence = new ReadOnlySequence<byte>(new byte[] { 1, 2, 3 }.AsMemory());
        Span<byte> dest = new byte[3];
        sequence.CopyTo(dest, out int writtenCount);
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

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(128)]
    [InlineData(124)]
    public static void StringBuilderToSequence(int stringLength)
    {
        var str = Random.Shared.NextString(Alphabet, stringLength);

        var builder = new StringBuilder();
        for (var i = 0; i < 3; i++)
        {
            builder.Append(str);
        }

        Equal(builder.ToString(), builder.ToReadOnlySequence().ToString());
    }

    [Fact]
    public static void CopyFromSequenceAndAdjustPosition()
    {
        var destination = new byte[10];
        ReadOnlySequence<byte> source = new(RandomBytes(16));

        var bytesWritten = source.CopyTo(destination, out SequencePosition consumed);
        Equal(bytesWritten, destination.Length);
        Equal(destination, source.Slice(0, consumed).ToArray());

        source = ToReadOnlySequence<byte>(RandomBytes(16), 3);
        bytesWritten = source.CopyTo(destination, out consumed);
        Equal(destination.Length, bytesWritten);
        Equal(destination, source.Slice(0, consumed).ToArray());

        source = ToReadOnlySequence<byte>(RandomBytes(22), 11);
        bytesWritten = source.CopyTo(destination, out consumed);
        Equal(destination.Length, bytesWritten);
        Equal(destination, source.Slice(0, consumed).ToArray());

        source = ToReadOnlySequence<byte>(RandomBytes(6), 3);
        bytesWritten = source.CopyTo(destination, out consumed);
        Equal(6, bytesWritten);
        Equal(source.End, consumed);
        Equal(destination.AsSpan(0, 6), source.Slice(0, consumed).ToArray().AsSpan());
    }
}
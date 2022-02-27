using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Buffers;

internal sealed class Chunk<T> : ReadOnlySequenceSegment<T>
{
    private Chunk(in ReadOnlyMemory<T> segment)
        => Memory = segment;

    private new Chunk<T> Next(in ReadOnlyMemory<T> segment)
    {
        var index = RunningIndex;
        Chunk<T> chunk;
        base.Next = chunk = new(segment) { RunningIndex = index + Memory.Length };
        return chunk;
    }

    internal static void AddChunk(in ReadOnlyMemory<T> segment, [AllowNull] ref Chunk<T> first, [AllowNull] ref Chunk<T> last)
    {
        Debug.Assert(!segment.IsEmpty);

        last = first is null || last is null
            ? first = new(segment) { RunningIndex = 0L }
            : last.Next(segment);
    }

    internal static ReadOnlySequence<T> CreateSequence(Chunk<T> head, Chunk<T> tail)
        => new(head, 0, tail, tail.Memory.Length);
}
using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Buffers
{
    internal sealed class Chunk<T> : ReadOnlySequenceSegment<T>
    {
        private Chunk(ReadOnlyMemory<T> segment)
            => Memory = segment;

        private new Chunk<T> Next(ReadOnlyMemory<T> segment)
        {
            var index = RunningIndex;
            Chunk<T> chunk;
            base.Next = chunk = new(segment) { RunningIndex = index + Memory.Length };
            return chunk;
        }

        internal static void AddChunk(ReadOnlyMemory<T> segment, [AllowNull] ref Chunk<T> first, [AllowNull] ref Chunk<T> last)
        {
            Debug.Assert(!segment.IsEmpty);

            if (first is null || last is null)
                first = last = new Chunk<T>(segment) { RunningIndex = 0L };
            else
                last = last.Next(segment);
        }

        internal static ReadOnlySequence<T> CreateSequence(Chunk<T> head, Chunk<T> tail)
            => new(head, 0, tail, tail.Memory.Length);
    }
}

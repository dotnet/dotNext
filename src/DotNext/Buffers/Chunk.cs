using System;
using System.Buffers;

namespace DotNext.Buffers
{
    internal sealed class Chunk<T> : ReadOnlySequenceSegment<T>
    {
        private Chunk(ReadOnlyMemory<T> segment)
        {
            RunningIndex = 0;
            Memory = segment;
        }

        private new Chunk<T> Next(ReadOnlyMemory<T> segment)
        {
            var index = RunningIndex;
            var chunk = new Chunk<T>(segment) { RunningIndex = index + Memory.Length };
            base.Next = chunk;
            return chunk;
        }

        internal static void AddChunk(ReadOnlyMemory<T> segment, ref Chunk<T> first, ref Chunk<T> last)
        {
            if (first is null || last is null)
                first = last = new Chunk<T>(segment);
            else
                last = last.Next(segment);
        }
    }
}

using System;

namespace DotNext.Buffers
{
    public partial class SequenceBuilder<T>
    {
        internal sealed class MemoryChunk : Disposable
        {
            private MemoryOwner<T> owner;
            private int writtenCount;

            internal MemoryChunk(MemoryAllocator<T>? allocator, int length, MemoryChunk? previous = null)
            {
                owner = allocator.Invoke(length, false);
                writtenCount = 0;
                if (!(previous is null))
                    previous.Next = this;
            }

            internal MemoryChunk? Next
            {
                get;
                private set;
            }

            public ReadOnlyMemory<T> Memory => owner.Memory.Slice(0, writtenCount);

            internal int Write(ReadOnlySpan<T> input)
            {
                var output = owner.Memory.Span.Slice(writtenCount);
                int count;
                if (input.IsEmpty || output.IsEmpty)
                    count = 0;
                else
                    input.CopyTo(output, out count);

                writtenCount += count;
                return count;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    Next = null;
                }

                owner.Dispose();
                owner = default;
                writtenCount = 0;
                base.Dispose(disposing);
            }
        }
    }
}
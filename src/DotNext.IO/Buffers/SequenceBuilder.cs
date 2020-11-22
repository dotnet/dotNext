using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Buffers
{
    /// <summary>
    /// Represents builder of non-contiguous memory buffer.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the memory.</typeparam>
    /// <seealso cref="PooledArrayBufferWriter{T}"/>
    /// <seealso cref="PooledBufferWriter{T}"/>
    public partial class SequenceBuilder<T> : Disposable, IReadOnlySequenceSource<T>, IEnumerable<ReadOnlyMemory<T>>, IGrowableBuffer<T>
    {
        private readonly int chunkSize;
        private readonly MemoryAllocator<T>? allocator;
        private MemoryChunk? first;

        [SuppressMessage("Usage", "CA2213", Justification = "It is implicitly through enumerating from first to last chunk in the chain")]
        private MemoryChunk? last;
        private long length;

        /// <summary>
        /// Initializes a new builder with the specified size of memory block.
        /// </summary>
        /// <param name="chunkSize">The size of the memory block representing single segment within sequence.</param>
        /// <param name="allocator">The allocator used to rent the segments.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="chunkSize"/> is less than or equal to zero.</exception>
        public SequenceBuilder(int chunkSize, MemoryAllocator<T>? allocator = null)
        {
            if (chunkSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(chunkSize));

            this.chunkSize = chunkSize;
            this.allocator = allocator;
        }

        /// <summary>
        /// Initializes a new builder with automatically selected
        /// chunk size.
        /// </summary>
        /// <param name="pool">Memory pool used to allocate memory chunks.</param>
        public SequenceBuilder(MemoryPool<T> pool)
        {
            chunkSize = -1;
            allocator = pool.ToAllocator();
        }

        /// <summary>
        /// Initializes a new builder which uses <see cref="MemoryPool{T}.Shared"/>
        /// as a default allocator of buffers.
        /// </summary>
        public SequenceBuilder()
            : this(MemoryPool<T>.Shared)
        {
        }

        /// <summary>
        /// Gets the number of written elements.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The builder has been disposed.</exception>
        public long WrittenCount
        {
            get
            {
                ThrowIfDisposed();
                return length;
            }
        }

        /// <summary>
        /// Writes the block of memory to this builder.
        /// </summary>
        /// <param name="input">The memory block to be written to this builder.</param>
        /// <exception cref="ObjectDisposedException">The builder has been disposed.</exception>
        public void Write(ReadOnlySpan<T> input)
        {
            ThrowIfDisposed();
            if (first is null || last is null)
                first = last = new MemoryChunk(allocator, chunkSize);

            for (int writtenCount; !input.IsEmpty; length += writtenCount)
            {
                writtenCount = last.Write(input);

                // no more space in the last chunk, allocate a new one
                if (writtenCount == 0)
                    last = new MemoryChunk(allocator, chunkSize, last);
                else
                    input = input.Slice(writtenCount);
            }
        }

        /// <summary>
        /// Writes a sequence of memory blocks to this builder.
        /// </summary>
        /// <param name="sequence">A sequence of memory blocks.</param>
        /// <exception cref="ObjectDisposedException">The builder has been disposed.</exception>
        public void Write(in ReadOnlySequence<T> sequence)
        {
            foreach (var segment in sequence)
                Write(segment.Span);
        }

        /// <summary>
        /// Constructs a sequence of memory blocks representing written content.
        /// </summary>
        /// <returns>A sequence of memory blocks representing written content.</returns>
        /// <exception cref="ObjectDisposedException">The builder has been disposed.</exception>
        public ReadOnlySequence<T> Build()
        {
            ThrowIfDisposed();
            return BufferHelpers.ToReadOnlySequence(this);
        }

        /// <summary>
        /// Passes the contents of this builder to the callback.
        /// </summary>
        /// <param name="writer">The callback used to accept memory segments representing the contents of this builder.</param>
        /// <param name="arg">The argument to be passed to the callback.</param>
        /// <typeparam name="TArg">The type of the argument to tbe passed to the callback.</typeparam>
        /// <exception cref="ObjectDisposedException">The builder has been disposed.</exception>
        public void CopyTo<TArg>(ReadOnlySpanAction<T, TArg> writer, TArg arg)
        {
            ThrowIfDisposed();
            for (MemoryChunk? current = first; !(current is null); current = current?.Next)
            {
                var buffer = current.Memory.Span;
                writer(buffer, arg);
            }
        }

        /// <summary>
        /// Copies the contents of this builder to the specified memory block.
        /// </summary>
        /// <param name="output">The memory block to be modified.</param>
        /// <returns>The actual number of copied elements.</returns>
        /// <exception cref="ObjectDisposedException">The builder has been disposed.</exception>
        public int CopyTo(Span<T> output)
        {
            ThrowIfDisposed();
            var total = 0;
            for (MemoryChunk? current = first; !(current is null) && !output.IsEmpty; current = current?.Next)
            {
                var buffer = current.Memory.Span;
                buffer.CopyTo(output, out var writtenCount);
                output = output.Slice(writtenCount);
                total += writtenCount;
            }

            return total;
        }

        /// <summary>
        /// Clears internal buffers so this builder can be reused.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The builder has been disposed.</exception>
        public void Clear()
        {
            ThrowIfDisposed();
            DisposeChunks();
            length = 0L;
        }

        /// <inheritdoc />
        ReadOnlySequence<T> IReadOnlySequenceSource<T>.Sequence => Build();

        /// <summary>
        /// Gets enumerator over memory segments.
        /// </summary>
        /// <returns>The enumerator over memory segments.</returns>
        public Enumerator GetEnumerator() => new Enumerator(first);

        /// <inheritdoc />
        IEnumerator<ReadOnlyMemory<T>> IEnumerable<ReadOnlyMemory<T>>.GetEnumerator()
            => GetEnumerator();

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private void DisposeChunks()
        {
            for (MemoryChunk? current = first, next; !(current is null); current = next)
            {
                next = current.Next;
                current.Dispose();
            }

            first = null;
            last = null;
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeChunks();
            }

            base.Dispose(disposing);
        }
    }
}
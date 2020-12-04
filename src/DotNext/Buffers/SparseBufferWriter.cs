using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Buffers
{
    /// <summary>
    /// Represents builder of the sparse memory buffer.
    /// </summary>
    /// <remarks>
    /// All members of <see cref="IBufferWriter{T}"/> are explicitly implemented because their
    /// usage can produce holes in the sparse buffer. To avoid holes, use public members only.
    /// </remarks>
    /// <typeparam name="T">The type of the elements in the memory.</typeparam>
    /// <seealso cref="PooledArrayBufferWriter{T}"/>
    /// <seealso cref="PooledBufferWriter{T}"/>
    [DebuggerDisplay("WrittenCount = {" + nameof(WrittenCount) + "}, FragmentedBytes = {" + nameof(FragmentedBytes) + "}")]
    public partial class SparseBufferWriter<T> : Disposable, IEnumerable<ReadOnlyMemory<T>>, IGrowableBuffer<T>, IConvertible<ReadOnlySequence<T>>
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
        public SparseBufferWriter(int chunkSize, MemoryAllocator<T>? allocator = null)
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
        public SparseBufferWriter(MemoryPool<T> pool)
        {
            chunkSize = -1;
            allocator = pool.ToAllocator();
        }

        /// <summary>
        /// Initializes a new builder which uses <see cref="MemoryPool{T}.Shared"/>
        /// as a default allocator of buffers.
        /// </summary>
        public SparseBufferWriter()
            : this(MemoryPool<T>.Shared)
        {
        }

        internal MemoryChunk? FirstChunk => first;

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

        private long FragmentedBytes
        {
            get
            {
                var result = 0L;
                for (MemoryChunk? current = first, next; !(current is null); current = next)
                {
                    next = current.Next;
                    if (!(next is null) && next.WrittenMemory.Length > 0)
                        result += current.FreeCapacity;
                }

                return result;
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
            if (last is null)
                first = last = new PooledMemoryChunk(allocator, chunkSize);

            for (int writtenCount; !input.IsEmpty; length += writtenCount)
            {
                writtenCount = last.Write(input);

                // no more space in the last chunk, allocate a new one
                if (writtenCount == 0)
                    last = new PooledMemoryChunk(allocator, chunkSize, last);
                else
                    input = input.Slice(writtenCount);
            }
        }

        /// <summary>
        /// Writes the block of memory to this builder.
        /// </summary>
        /// <param name="input">The memory block to be written to this builder.</param>
        /// <param name="copyMemory"><see langword="true"/> to copy the content of the input buffer; <see langword="false"/> to import the memory block.</param>
        /// <exception cref="ObjectDisposedException">The builder has been disposed.</exception>
        public void Write(ReadOnlyMemory<T> input, bool copyMemory = true)
        {
            ThrowIfDisposed();
            if (input.IsEmpty)
                goto exit;

            if (copyMemory)
            {
                Write(input.Span);
            }
            else if (last is null)
            {
                first = last = new ImportedMemoryChunk(input);
                length += input.Length;
            }
            else
            {
                last = new ImportedMemoryChunk(input, last);
                length += input.Length;
            }

            exit:
            return;
        }

        /// <summary>
        /// Writes a sequence of memory blocks to this builder.
        /// </summary>
        /// <param name="sequence">A sequence of memory blocks.</param>
        /// <param name="copyMemory"><see langword="true"/> to copy the content of the input buffer; <see langword="false"/> to import memory blocks.</param>
        /// <exception cref="ObjectDisposedException">The builder has been disposed.</exception>
        public void Write(in ReadOnlySequence<T> sequence, bool copyMemory = true)
        {
            foreach (var segment in sequence)
                Write(segment, copyMemory);
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
            for (MemoryChunk? current = first; !(current is null); current = current.Next)
            {
                var buffer = current.WrittenMemory.Span;
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
            for (MemoryChunk? current = first; !(current is null) && !output.IsEmpty; current = current.Next)
            {
                var buffer = current.WrittenMemory.Span;
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
            ReleaseChunks();
            length = 0L;
        }

        /// <inheritdoc />
        ReadOnlySequence<T> IConvertible<ReadOnlySequence<T>>.Convert()
        {
            if (first is null)
                return ReadOnlySequence<T>.Empty;

            if (first.Next is null)
                return new ReadOnlySequence<T>(first.WrittenMemory);

            return BufferHelpers.ToReadOnlySequence(this);
        }

        /// <summary>
        /// Gets enumerator over memory segments.
        /// </summary>
        /// <returns>The enumerator over memory segments.</returns>
        /// <exception cref="ObjectDisposedException">The builder has been disposed.</exception>
        public Enumerator GetEnumerator()
        {
            ThrowIfDisposed();
            return new Enumerator(first);
        }

        /// <inheritdoc />
        IEnumerator<ReadOnlyMemory<T>> IEnumerable<ReadOnlyMemory<T>>.GetEnumerator()
            => GetEnumerator();

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private void ReleaseChunks()
        {
            for (MemoryChunk? current = first, next; !(current is null); current = next)
            {
                next = current.Next;
                current.Dispose();
            }

            first = last = null;
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ReleaseChunks();
            }

            base.Dispose(disposing);
        }
    }
}
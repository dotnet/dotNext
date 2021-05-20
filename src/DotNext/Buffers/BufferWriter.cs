using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Buffers
{
    using Seq = Collections.Generic.Sequence;

    /// <summary>
    /// Represents memory-backed output sink which <typeparamref name="T"/> data can be written.
    /// </summary>
    /// <typeparam name="T">The data type that can be written.</typeparam>
    [DebuggerDisplay("WrittenCount = {" + nameof(WrittenCount) + "}, FreeCapacity = {" + nameof(FreeCapacity) + "}")]
    public abstract class BufferWriter<T> : Disposable, IBufferWriter<T>, ISupplier<ReadOnlyMemory<T>>, IReadOnlyList<T>, IGrowableBuffer<T>
    {
        private object? diagObj;

        /// <summary>
        /// Represents position of write cursor.
        /// </summary>
        private protected int position;

        /// <summary>
        /// Initializes a new memory writer.
        /// </summary>
        private protected BufferWriter()
        {
        }

        /// <summary>
        /// Sets the counter used to report allocation of internal buffer.
        /// </summary>
        [DisallowNull]
        public EventCounter? AllocationCounter
        {
            private protected get => diagObj as EventCounter;
            set => diagObj = value;
        }

        /// <summary>
        /// Sets the callback used internally to report actual size
        /// of allocated buffer.
        /// </summary>
        [DisallowNull]
        public Action<int>? BufferSizeCallback
        {
            private protected get => diagObj as Action<int>;
            set => diagObj = value;
        }

        /// <summary>
        /// Gets the data written to the underlying buffer so far.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public abstract ReadOnlyMemory<T> WrittenMemory { get; }

        /// <inheritdoc/>
        ReadOnlyMemory<T> ISupplier<ReadOnlyMemory<T>>.Invoke() => WrittenMemory;

        /// <summary>
        /// Gets the amount of data written to the underlying memory so far.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public int WrittenCount
        {
            get
            {
                ThrowIfDisposed();
                return position;
            }
        }

        /// <inheritdoc />
        long IGrowableBuffer<T>.WrittenCount => WrittenCount;

        /// <inheritdoc />
        void IGrowableBuffer<T>.Write(ReadOnlySpan<T> input)
            => BuffersExtensions.Write(this, input);

        /// <inheritdoc />
        void IGrowableBuffer<T>.CopyTo<TConsumer>(TConsumer consumer)
            => consumer.Invoke(WrittenMemory.Span);

        /// <inheritdoc />
        void IGrowableBuffer<T>.Clear() => Clear();

        /// <inheritdoc />
        int IGrowableBuffer<T>.CopyTo(Span<T> output)
        {
            WrittenMemory.Span.CopyTo(output, out var writtenCount);
            return writtenCount;
        }

        /// <inheritdoc />
        bool IGrowableBuffer<T>.TryGetWrittenContent(out ReadOnlyMemory<T> block)
        {
            block = WrittenMemory;
            return true;
        }

        /// <inheritdoc />
        ValueTask IGrowableBuffer<T>.CopyToAsync<TConsumer>(TConsumer consumer, CancellationToken token)
            => IsDisposed ? new ValueTask(DisposedTask) : consumer.Invoke(WrittenMemory, token);

        /// <summary>
        /// Writes single element.
        /// </summary>
        /// <param name="item">The element to write.</param>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public void Add(T item)
        {
            GetSpan(1)[0] = item;
            Advance(1);
        }

        /// <summary>
        /// Writes multiple elements.
        /// </summary>
        /// <param name="items">The collection of elements to be copied.</param>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public void AddAll(ICollection<T> items)
        {
            if (items.Count == 0)
                return;

            var span = GetSpan(items.Count);
            int count;
            using (var enumerator = items.GetEnumerator())
            {
                for (count = 0; count < items.Count && enumerator.MoveNext(); count++)
                    span[count] = enumerator.Current;
            }

            Advance(count);
        }

        /// <inheritdoc/>
        int IReadOnlyCollection<T>.Count => WrittenCount;

        /// <summary>
        /// Gets the element at the specified index.
        /// </summary>
        /// <param name="index">The index of the element to retrieve.</param>
        /// <value>The element at the specified index.</value>
        /// <exception cref="IndexOutOfRangeException"><paramref name="index"/> the index is invalid.</exception>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public ref readonly T this[int index] => ref WrittenMemory.Span[index];

        /// <inheritdoc/>
        T IReadOnlyList<T>.this[int index] => this[index];

        /// <summary>
        /// Gets the total amount of space within the underlying memory.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public abstract int Capacity { get; }

        /// <summary>
        /// Gets the amount of space available that can still be written into without forcing the underlying buffer to grow.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public int FreeCapacity
        {
            get
            {
                ThrowIfDisposed();
                return Capacity - WrittenCount;
            }
        }

        /// <summary>
        /// Clears the data written to the underlying memory.
        /// </summary>
        /// <param name="reuseBuffer"><see langword="true"/> to reuse the internal buffer; <see langword="false"/> to destroy the internal buffer.</param>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public abstract void Clear(bool reuseBuffer = false);

        /// <summary>
        /// Notifies this writer that <paramref name="count"/> of data items were written.
        /// </summary>
        /// <param name="count">The number of data items written to the underlying buffer.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero.</exception>
        /// <exception cref="InvalidOperationException">Attempts to advance past the end of the underlying buffer.</exception>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public void Advance(int count)
        {
            ThrowIfDisposed();
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (position > Capacity - count)
                throw new InvalidOperationException();
            position += count;
        }

        /// <summary>
        /// Returns the memory to write to that is at least the requested size.
        /// </summary>
        /// <param name="sizeHint">The minimum length of the returned memory.</param>
        /// <returns>The memory block of at least the size <paramref name="sizeHint"/>.</returns>
        /// <exception cref="OutOfMemoryException">The requested buffer size is not available.</exception>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public abstract Memory<T> GetMemory(int sizeHint = 0);

        /// <summary>
        /// Returns the memory to write to that is at least the requested size.
        /// </summary>
        /// <param name="sizeHint">The minimum length of the returned memory.</param>
        /// <returns>The memory block of at least the size <paramref name="sizeHint"/>.</returns>
        /// <exception cref="OutOfMemoryException">The requested buffer size is not available.</exception>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public virtual Span<T> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;

        /// <summary>
        /// Transfers ownership of the written memory from this writer to the caller.
        /// </summary>
        /// <remarks>
        /// The caller is responsible for the lifetime of the returned buffer. The current
        /// state of this writer will be reset.
        /// </remarks>
        /// <returns>The object representing all written content.</returns>
        /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
        public abstract MemoryOwner<T> DetachBuffer();

        /// <summary>
        /// Reallocates internal buffer.
        /// </summary>
        /// <param name="newSize">A new size of internal buffer.</param>
        private protected abstract void Resize(int newSize);

        /// <summary>
        /// Ensures capacity of internal buffer.
        /// </summary>
        /// <param name="sizeHint">The requested size of the buffer.</param>
        private protected void CheckAndResizeBuffer(int sizeHint)
        {
            var newSize = IGrowableBuffer<T>.GetBufferSize(sizeHint, Capacity, position);
            if (newSize.HasValue)
                Resize(newSize.GetValueOrDefault());
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                diagObj = null;
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Gets enumerator over all written elements.
        /// </summary>
        /// <returns>The enumerator over all written elements.</returns>
        public IEnumerator<T> GetEnumerator() => Seq.ToEnumerator(WrittenMemory);

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Gets the textual representation of this buffer.
        /// </summary>
        /// <returns>The textual representation of this buffer.</returns>
        public override string ToString() => WrittenMemory.ToString();
    }
}
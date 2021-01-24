using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using Buffers;

    /// <summary>
    /// Adds a buffering layer to write operations on underlying stream.
    /// </summary>
    /// <remarks>
    /// In contrast to <see cref="System.IO.BufferedStream"/>, this type
    /// supports custom memory allocator for the internal buffer. The buffer can be represented
    /// by rented memory from the pool. Additionally, this writen is not restricted by <see cref="byte"/> value
    /// type as a representation of input data.
    /// </remarks>
    /// <typeparam name="T">The type of the elements in the buffer.</typeparam>
    public abstract class BufferedWriter<T> : Disposable, IFlushableBufferWriter<T>
        where T : struct
    {
        private readonly PooledBufferWriter<T> buffer;

        private protected BufferedWriter(MemoryAllocator<T>? allocator)
        {
            buffer = new PooledBufferWriter<T>(allocator);
        }

        /// <summary>
        /// Clears the internal buffer of this writer and causes any buffered data to be written
        /// to the underlying stream.
        /// </summary>
        /// <param name="flushStream">
        /// <see langword="true"/> to call <see cref="System.IO.Stream.Flush()"/> or similar method after writting the buffered data to the underlying stream;
        /// <see langword="false"/> to write the buffered data to the underlying stream.
        /// </param>
        public abstract void Flush(bool flushStream);

        /// <inheritdoc />
        void IFlushable.Flush() => Flush(false);

        private protected void Flush<TWriter>(ref TWriter output, bool flush)
            where TWriter : struct, IReadOnlySpanConsumer<T>, IFlushable
        {
            output.Invoke(buffer.WrittenMemory.Span);
            buffer.Clear(true);
            if (flush)
                output.Flush();
        }

        /// <summary>
        /// Clears the internal buffer of this writer and causes any buffered data to be written
        /// to the underlying stream.
        /// </summary>
        /// <param name="flushStream">
        /// <see langword="true"/> to call <see cref="System.IO.Stream.Flush()"/> or similar method after writting the buffered data to the underlying stream;
        /// <see langword="false"/> to write the buffered data to the underlying stream.
        /// </param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        public abstract Task FlushAsync(bool flushStream, CancellationToken token = default);

        /// <inheritdoc />
        Task IFlushable.FlushAsync(CancellationToken token) => FlushAsync(false, token);

        private protected async Task FlushAsync<TWriter>(TWriter output, bool flush, CancellationToken token)
            where TWriter : struct, IReadOnlySpanConsumer<T>, IFlushable
        {
            await output.Invoke(buffer.WrittenMemory, token).ConfigureAwait(false);
            buffer.Clear(true);
            if (flush)
                await output.FlushAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the block of memory from the internal buffer.
        /// </summary>
        /// <param name="sizeHint">The size of the requested memory block.</param>
        /// <returns>The memory block of at least the specified size.</returns>
        public Memory<T> GetMemory(int sizeHint = 0) => buffer.GetMemory(sizeHint);

        /// <summary>
        /// Gets the block of memory from the internal buffer.
        /// </summary>
        /// <param name="sizeHint">The size of the requested memory block.</param>
        /// <returns>The memory block of at least the specified size.</returns>
        public Span<T> GetSpan(int sizeHint = 0) => buffer.GetSpan(sizeHint);

        /// <summary>
        /// Notifies this writer that <paramref name="count"/> of data items were written to the internal buffer.
        /// </summary>
        /// <param name="count">The number of data items written to the underlying buffer.</param>
        public void Advance(int count) => buffer.Advance(count);

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                buffer.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    internal sealed class BufferedWriter<T, TWriter> : BufferedWriter<T>
        where T : struct
        where TWriter : struct, IReadOnlySpanConsumer<T>, IFlushable
    {
        private TWriter writer;

        internal BufferedWriter(TWriter writer, MemoryAllocator<T>? allocator)
            : base(allocator)
        {
            this.writer = writer;
        }

        public override void Flush(bool flushStream)
            => Flush(ref writer, flushStream);

        public override Task FlushAsync(bool flushStream, CancellationToken token)
            => FlushAsync(writer, flushStream, token);

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                writer = default;
            }

            base.Dispose(disposing);
        }
    }
}
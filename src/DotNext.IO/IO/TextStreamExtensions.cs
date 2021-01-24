using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using Buffers;

    /// <summary>
    /// Represents various extension methods for <see cref="TextWriter"/> and <see cref="TextReader"/> classes.
    /// </summary>
    public static class TextStreamExtensions
    {
        /// <summary>
        /// Creates text writer backed by the char buffer writer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="provider">The object that controls formatting.</param>
        /// <param name="flush">The optional implementation of <see cref="TextWriter.Flush"/> method.</param>
        /// <param name="flushAsync">The optional implementation of <see cref="TextWriter.FlushAsync"/> method.</param>
        /// <typeparam name="TWriter">The type of the char buffer writer.</typeparam>
        /// <returns>The text writer backed by the buffer writer.</returns>
        public static TextWriter AsTextWriter<TWriter>(this TWriter writer, IFormatProvider? provider = null, Action<TWriter>? flush = null, Func<TWriter, CancellationToken, Task>? flushAsync = null)
            where TWriter : class, IBufferWriter<char>
        {
            IFlushable.DiscoverFlushMethods(writer, ref flush, ref flushAsync);
            return new TextBufferWriter<TWriter>(writer, provider, flush, flushAsync);
        }

        /// <summary>
        /// Adds a buffering layer to write operations on another writer and make it compatible with
        /// <see cref="System.Buffers.IBufferWriter{T}"/> interface.
        /// </summary>
        /// <param name="output">The writer to convert.</param>
        /// <param name="allocator">The allocator of the buffer.</param>
        /// <returns>The buffered text writer.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
        public static BufferedWriter<char> AsBufferWriter(this TextWriter output, MemoryAllocator<char>? allocator = null)
            => new BufferedWriter<char, TextConsumer>(output, allocator);

        /// <summary>
        /// Creates <see cref="TextReader"/> over the sequence of characters.
        /// </summary>
        /// <param name="sequence">The sequence of characters.</param>
        /// <returns>The reader over the sequence of characters.</returns>
        public static TextReader AsTextReader(this ReadOnlySequence<char> sequence)
            => new TextBufferReader(sequence);
    }
}
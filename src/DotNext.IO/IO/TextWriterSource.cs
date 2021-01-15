using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    /// <summary>
    /// Represents <see cref="TextWriter"/> factory methods.
    /// </summary>
    public static class TextWriterSource
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
            if (writer is IFlushable)
            {
                flush ??= Flush;
                flushAsync ??= FlushAsync;
            }

            return new TextBufferWriter<TWriter>(writer, provider, flush, flushAsync);

            static void Flush(TWriter writer)
            {
                Debug.Assert(writer is IFlushable);
                Unsafe.As<IFlushable>(writer).Flush();
            }

            static Task FlushAsync(TWriter writer, CancellationToken token)
            {
                Debug.Assert(writer is IFlushable);
                return Unsafe.As<IFlushable>(writer).FlushAsync(token);
            }
        }
    }
}
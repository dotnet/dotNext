using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    /// <summary>
    /// Represents extension methods for <see cref="TextWriter"/> class.
    /// </summary>
    public static class TextWriterExtensions
    {
        /// <summary>
        /// Asynchronously writes a linked regions of characters to the text stream.
        /// </summary>
        /// <param name="writer">The stream to write into.</param>
        /// <param name="chars">The linked regions of characters.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async ValueTask WriteAsync(this TextWriter writer, ReadOnlySequence<char> chars, CancellationToken token = default)
        {
            foreach (var segment in chars)
                await writer.WriteAsync(segment, token).ConfigureAwait(false);
        }
    }
}
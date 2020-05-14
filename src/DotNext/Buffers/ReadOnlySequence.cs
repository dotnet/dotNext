using System.Buffers;
using System.IO;

namespace DotNext.Buffers
{
    /// <summary>
    /// Represents helper methods to work with <see cref="ReadOnlySequence{T}"/> data type.
    /// </summary>
    public static class ReadOnlySequence
    {
        /// <summary>
        /// Converts read-only sequence of bytes to the read-only stream.
        /// </summary>
        /// <param name="sequence">The sequence of bytes.</param>
        /// <returns>The stream over sequence of bytes.</returns>
        public static Stream AsStream(this ReadOnlySequence<byte> sequence)
            => new ReadOnlyMemoryStream(sequence);
    }
}
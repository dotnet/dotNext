using System;
using System.Buffers;
using System.IO;

namespace DotNext.Buffers
{
    /// <summary>
    /// Represents conversion of various buffer types to stream.
    /// </summary>
    public static class StreamSource
    {
        /// <summary>
        /// Converts read-only sequence of bytes to a read-only stream.
        /// </summary>
        /// <param name="sequence">The sequence of bytes.</param>
        /// <returns>The stream over sequence of bytes.</returns>
        public static Stream AsStream(this ReadOnlySequence<byte> sequence)
            => new ReadOnlyMemoryStream(sequence);
        
        /// <summary>
        /// Converts read-only memory to a read-only stream.
        /// </summary>
        /// <param name="memory">The read-only memory.</param>
        /// <returns>The stream over memory of bytes.</returns>
        public static Stream AsStream(this ReadOnlyMemory<byte> memory)
            => AsStream(new ReadOnlySequence<byte>(memory));
        
        private static MemoryStream CreateStream(byte[] buffer, int length)
            => new MemoryStream(buffer, 0, length, false, false);

        /// <summary>
        /// Gets written content as a read-only stream.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <returns>The stream representing written bytes.</returns>
        public static Stream AsStream(this PooledArrayBufferWriter<byte> writer)
            => writer.WrapBuffer(new ValueFunc<byte[], int, MemoryStream>(CreateStream));
    }
}
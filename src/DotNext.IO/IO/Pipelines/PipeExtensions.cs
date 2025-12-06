using System.IO.Pipelines;

namespace DotNext.IO.Pipelines;

/// <summary>
/// Represents extension method for parsing data stored in pipe.
/// </summary>
public static partial class PipeExtensions
{
    /// <summary>
    /// Creates a duplex stream suitable for reading and writing from the duplex pipe.
    /// </summary>
    /// <param name="pipe">The duplex pipe.</param>
    /// <param name="leaveInputOpen"><see langword="true"/> to leave <see cref="IDuplexPipe.Input"/> available for reads; otherwise, <see langword="false"/>.</param>
    /// <param name="leaveOutputOpen"><see langword="true"/> to leave <see cref="IDuplexPipe.Output"/> available for writes; otherwise, <see langword="false"/>.</param>
    /// <returns>The stream that can be used to read from and write to the pipe.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pipe"/> is <see langword="null"/>.</exception>
    public static Stream AsStream(this IDuplexPipe pipe, bool leaveInputOpen = false, bool leaveOutputOpen = false)
    {
        ArgumentNullException.ThrowIfNull(pipe);

        return new DuplexStream(pipe, leaveInputOpen, leaveOutputOpen);
    }

    /// <summary>
    /// Extends <see cref="IAsyncBinaryReader"/> type.
    /// </summary>
    extension(IAsyncBinaryReader)
    {
        /// <summary>
        /// Creates default implementation of binary reader for the specified pipe reader.
        /// </summary>
        /// <remarks>
        /// It is recommended to use extension methods from <see cref="Pipelines.PipeExtensions"/> class
        /// for decoding data from the stream. This method is intended for situation
        /// when you need an object implementing <see cref="IAsyncBinaryReader"/> interface.
        /// </remarks>
        /// <param name="reader">The pipe reader.</param>
        /// <returns>The binary reader.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>.</exception>
        public static IAsyncBinaryReader Create(PipeReader reader) => new PipeBinaryReader(reader);
    }

    /// <summary>
    /// Extends <see cref="IAsyncBinaryWriter"/> type.
    /// </summary>
    extension(IAsyncBinaryWriter)
    {
        /// <summary>
        /// Creates default implementation of binary writer for the pipe.
        /// </summary>
        /// <param name="output">The stream instance.</param>
        /// <param name="bufferSize">The maximum numbers of bytes that can be buffered in the memory without flushing.</param>
        /// <returns>The binary writer.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="bufferSize"/> or is less than or equal to zero.</exception>
        public static IAsyncBinaryWriter Create(PipeWriter output, long bufferSize = 0L)
        {
            ArgumentNullException.ThrowIfNull(output);
            ArgumentOutOfRangeException.ThrowIfNegative(bufferSize);

            return new PipeBinaryWriter(output, bufferSize);
        }
    }
}
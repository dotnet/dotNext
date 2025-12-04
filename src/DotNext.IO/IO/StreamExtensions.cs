using System.Runtime.CompilerServices;
using Microsoft.Win32.SafeHandles;

namespace DotNext.IO;

using Buffers;

/// <summary>
/// Represents high-level read/write methods for the stream.
/// </summary>
/// <remarks>
/// This class provides alternative way to read and write typed data from/to the stream
/// without instantiation of <see cref="BinaryReader"/> and <see cref="BinaryWriter"/>.
/// </remarks>
public static partial class StreamExtensions
{
    /// <summary>
    /// Extends <see cref="Stream"/> type.
    /// </summary>
    extension(Stream stream)
    {
        /// <summary>
        /// Combines multiple readable streams.
        /// </summary>
        /// <param name="streams">A collection of streams.</param>
        /// <param name="leaveOpen"><see langword="true"/> to keep the wrapped streams alive when combined stream disposed; otherwise, <see langword="false"/>.</param>
        /// <returns>An object that represents multiple streams as one logical stream.</returns>
        /// <exception cref="ArgumentException"><paramref name="streams"/> is empty.</exception>
        public static Stream Combine(ReadOnlySpan<Stream> streams, bool leaveOpen = true)
        {
            return streams switch
            {
                [] => Stream.Null,
                [var s] => s,
                [var s1, var s2] => SparseStream.Create<(Stream, Stream)>((s1, s2), leaveOpen),
                [var s1, var s2, var s3] => SparseStream.Create<(Stream, Stream, Stream)>((s1, s2, s3), leaveOpen),
                [var s1, var s2, var s3, var s4] => SparseStream.Create<(Stream, Stream, Stream, Stream)>((s1, s2, s3, s4), leaveOpen),
                [var s1, var s2, var s3, var s4, var s5] => SparseStream.Create<(Stream, Stream, Stream, Stream, Stream)>((s1, s2, s3, s4,
                    s5), leaveOpen),
                [var s1, var s2, var s3, var s4, var s5, var s6] => SparseStream.Create<(Stream, Stream, Stream, Stream, Stream, Stream)>((s1, s2, s3,
                    s4,
                    s5, s6), leaveOpen),
                _ => SparseStream.Create(streams, leaveOpen),
            };
        }
        
        /// <summary>
        /// Combines multiple readable streams.
        /// </summary>
        /// <param name="streams">A collection of streams.</param>
        /// <param name="leaveOpen"><see langword="true"/> to keep the wrapped streams alive when combined stream disposed; otherwise, <see langword="false"/>.</param>
        /// <returns>An object that represents multiple streams as one logical stream.</returns>
        /// <exception cref="ArgumentException"><paramref name="streams"/> is empty.</exception>
        public static Stream Combine(IEnumerable<Stream> streams, bool leaveOpen = true)
        {
            // Use buffer to allocate streams on the stack
            var buffer = new StreamBuffer();
            var writer = new BufferWriterSlim<Stream>(buffer);

            Stream result;
            try
            {
                writer += streams;
                result = Combine(writer.WrittenSpan, leaveOpen);
            }
            finally
            {
                writer.Dispose();
            }

            return result;
        }

        /// <summary>
        /// Creates a stream wrapper over the memory block.
        /// </summary>
        /// <param name="memory">The memory block to wrap.</param>
        /// <param name="skipOnOverflow">
        /// Indicates that <see cref="Stream.Write(ReadOnlySpan{byte})"/> and <see cref="Stream.WriteAsync(ReadOnlyMemory{byte}, CancellationToken)"/> must throw
        /// <see cref="IOException"/> if the caller is trying to write past to the end of the underlying buffer.
        /// </param>
        /// <returns>The wrapper stream.</returns>
        public static MemorySegmentStream Create(Memory<byte> memory, bool skipOnOverflow = false)
            => new(memory) { SkipOnOverflow = false };

        /// <summary>
        /// Creates a slice over the specified stream.
        /// </summary>
        /// <param name="offset">The offset within a stream.</param>
        /// <param name="length">The length of the segment.</param>
        /// <returns>The slice over the stream.</returns>
        public StreamSegment Slice(long offset, long length)
            => new(stream) { Range = (offset, length) };
    }

    /// <summary>
    /// Creates a stream for the specified file handle.
    /// </summary>
    /// <remarks>
    /// The returned stream doesn't own the handle.
    /// </remarks>
    /// <param name="handle">The file handle.</param>
    /// <param name="access">Desired access to the file via stream.</param>
    /// <returns>The unbuffered file stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handle"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="handle"/> is closed or invalid.</exception>
    public static Stream AsUnbufferedStream(this SafeFileHandle handle, FileAccess access)
    {
        ArgumentNullException.ThrowIfNull(handle);

        return handle is { IsInvalid: false, IsClosed: false }
            ? new UnbufferedFileStream(handle, access)
            : throw new ArgumentException(ExceptionMessages.FileHandleClosed, nameof(handle));
    }

    /// <summary>
    /// Extends <see cref="IAsyncBinaryReader"/> type.
    /// </summary>
    extension(IAsyncBinaryReader)
    {
        /// <summary>
        /// Creates default implementation of binary reader for the stream.
        /// </summary>
        /// <remarks>
        /// It is recommended to use extension methods from <see cref="StreamExtensions"/> class
        /// for decoding data from the stream. This method is intended for situation
        /// when you need an object implementing <see cref="IAsyncBinaryReader"/> interface.
        /// </remarks>
        /// <param name="input">The stream to be wrapped into the reader.</param>
        /// <param name="buffer">The buffer used for decoding data from the stream.</param>
        /// <returns>The stream reader.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> is empty.</exception>
        public static IAsyncBinaryReader Create(Stream input, Memory<byte> buffer)
            => ReferenceEquals(input, Stream.Null) ? IAsyncBinaryReader.Empty : new AsyncStreamBinaryAccessor(input, buffer);
    }
    
    [InlineArray(32)]
    private struct StreamBuffer
    {
        private Stream element0;
    }
}
using System.Runtime.CompilerServices;
using Microsoft.Win32.SafeHandles;

namespace DotNext.IO;

using Buffers;
using static Runtime.Intrinsics;

/// <summary>
/// Represents high-level read/write methods for the stream.
/// </summary>
/// <remarks>
/// This class provides alternative way to read and write typed data from/to the stream
/// without instantiation of <see cref="BinaryReader"/> and <see cref="BinaryWriter"/>.
/// </remarks>
public static partial class StreamExtensions
{
    internal static void ThrowIfEmpty<T>(in Memory<T> buffer, [CallerArgumentExpression(nameof(buffer))] string expression = "buffer")
    {
        if (buffer.IsEmpty)
            throw new ArgumentException(ExceptionMessages.BufferTooSmall, expression);
    }

    private static Stream Combine(Stream stream, ReadOnlySpan<Stream> others, bool leaveOpen)
        => others switch
        {
            [] => stream,
            [var s] => SparseStream.Create<(Stream, Stream)>((stream, s), leaveOpen),
            [var s1, var s2] => SparseStream.Create<(Stream, Stream, Stream)>((stream, s1, s2), leaveOpen),
            [var s1, var s2, var s3] => SparseStream.Create<(Stream, Stream, Stream, Stream)>((stream, s1, s2, s3), leaveOpen),
            [var s1, var s2, var s3, var s4] => SparseStream.Create<(Stream, Stream, Stream, Stream, Stream)>((stream, s1, s2, s3, s4), leaveOpen),
            [var s1, var s2, var s3, var s4, var s5] => SparseStream.Create<(Stream, Stream, Stream, Stream, Stream, Stream)>((stream, s1, s2, s3, s4,
                s5), leaveOpen),
            [var s1, var s2, var s3, var s4, var s5, var s6] => SparseStream.Create<(Stream, Stream, Stream, Stream, Stream, Stream, Stream)>((stream,
                s1, s2, s3, s4,
                s5, s6), leaveOpen),
            { Length: int.MaxValue } => throw new InsufficientMemoryException(),
            _ => SparseStream.Create(stream, others, leaveOpen),
        };

    /// <summary>
    /// Combines multiple readable streams.
    /// </summary>
    /// <param name="stream">The stream to combine.</param>
    /// <param name="others">A collection of streams.</param>
    /// <returns>An object that represents multiple streams as one logical stream.</returns>
    public static Stream Combine(this Stream stream, ReadOnlySpan<Stream> others) // TODO: Use params in future
        => Combine(stream, others, leaveOpen: true);

    /// <summary>
    /// Combines multiple readable streams.
    /// </summary>
    /// <param name="streams">A collection of streams.</param>
    /// <param name="leaveOpen"><see langword="true"/> to keep the wrapped streams alive when combined stream disposed; otherwise, <see langword="false"/>.</param>
    /// <returns>An object that represents multiple streams as one logical stream.</returns>
    /// <exception cref="ArgumentException"><paramref name="streams"/> is empty.</exception>
    public static Stream Combine(this ReadOnlySpan<Stream> streams, bool leaveOpen = true)
        => streams is [var first, .. var rest]
            ? Combine(first, rest, leaveOpen)
            : throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(streams));

    /// <summary>
    /// Combines multiple readable streams.
    /// </summary>
    /// <param name="streams">A collection of streams.</param>
    /// <param name="leaveOpen"><see langword="true"/> to keep the wrapped streams alive when combined stream disposed; otherwise, <see langword="false"/>.</param>
    /// <returns>An object that represents multiple streams as one logical stream.</returns>
    /// <exception cref="ArgumentException"><paramref name="streams"/> is empty.</exception>
    public static Stream Combine(this IEnumerable<Stream> streams, bool leaveOpen = true)
    {
        // Use buffer to allocate streams on the stack
        var buffer = new StreamBuffer();
        var writer = new BufferWriterSlim<Stream>(buffer);

        Stream result;
        try
        {
            writer.AddAll(streams);
            result = Combine(writer.WrittenSpan, leaveOpen);
        }
        finally
        {
            writer.Dispose();
            KeepAlive(in buffer);
        }

        return result;
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
    
    [InlineArray(32)]
    private struct StreamBuffer
    {
        private Stream element0;
    }
}
using System.Runtime.CompilerServices;
using Microsoft.Win32.SafeHandles;

namespace DotNext.IO;

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

    /// <summary>
    /// Combines multiple readable streams.
    /// </summary>
    /// <param name="stream">The stream to combine.</param>
    /// <param name="others">A collection of streams.</param>
    /// <returns>An object that represents multiple streams as one logical stream.</returns>
    public static Stream Combine(this Stream stream, ReadOnlySpan<Stream> others)
        => others is { Length: > 0 } ? new SparseStream([stream, .. others]) : stream;

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
}
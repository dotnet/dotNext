using System.Runtime.CompilerServices;

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
}
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

/// <summary>
/// Represents interpolated string builder that utilizes reusable buffer rented from the pool.
/// </summary>
[InterpolatedStringHandler]
[EditorBrowsable(EditorBrowsableState.Never)]
[StructLayout(LayoutKind.Auto)]
public struct PoolingInterpolatedStringHandler
{
    private readonly IFormatProvider? provider;
    private InlinedBufferWriter<char> writer;

    /// <summary>
    /// Initializes a new interpolated string handler.
    /// </summary>
    /// <param name="literalLength">The total number of characters in known at compile-time.</param>
    /// <param name="formattedCount">The number of placeholders.</param>
    /// <param name="allocator">The buffer allocator.</param>
    /// <param name="provider">Optional formatting provider.</param>
    public PoolingInterpolatedStringHandler(int literalLength, int formattedCount, MemoryAllocator<char>? allocator, IFormatProvider? provider = null)
    {
        // assume that every placeholder will be converted to substring no longer than X chars
        const int charsPerPlaceholder = 10;
        var length = (charsPerPlaceholder * formattedCount) + literalLength;

        writer = (uint)length <= (uint)Array.MaxLength
            ? new(allocator) { Capacity = length }
            : throw new InsufficientMemoryException();

        this.provider = provider;
    }

    internal MemoryOwner<char> DetachBuffer() => writer.DetachBuffer();

    /// <summary>
    /// Writes the specified string to the handler.
    /// </summary>
    /// <param name="value">The string to write.</param>
    public void AppendLiteral(string? value)
        => AppendFormatted(value.AsSpan());

    /// <summary>
    /// Writes the specified value to the handler.
    /// </summary>
    /// <typeparam name="T">The type of the value to write.</typeparam>
    /// <param name="value">The value to write.</param>
    /// <param name="format">The format string.</param>
    public void AppendFormatted<T>(T value, string? format = null)
        => CharBuffer.AppendFormatted<T, InlinedBufferWriter<char>.Ref>(new(ref writer), value, format, provider);

    /// <summary>
    /// Writes the specified string of chars to the handler.
    /// </summary>
    /// <param name="value">The span to write.</param>
    /// <param name="alignment">
    /// Minimum number of characters that should be written for this value. If the value is negative,
    /// it indicates left-aligned and the required minimum is the absolute value.
    /// </param>
    public void AppendFormatted(ReadOnlySpan<char> value, int alignment)
        => CharBuffer.AppendFormatted<InlinedBufferWriter<char>.Ref>(new(ref writer), value, alignment);

    /// <summary>
    /// Writes the specified value to the handler.
    /// </summary>
    /// <typeparam name="T">The type of the value to write.</typeparam>
    /// <param name="value">The value to write.</param>
    /// <param name="alignment">
    /// Minimum number of characters that should be written for this value. If the value is negative,
    /// it indicates left-aligned and the required minimum is the absolute value.
    /// </param>
    /// <param name="format">The format string.</param>
    public void AppendFormatted<T>(T value, int alignment, string? format = null)
        => CharBuffer.AppendFormatted<T, InlinedBufferWriter<char>.Ref>(new(ref writer), value, alignment, format, provider);

    /// <summary>
    /// Writes the specified character span to the handler.
    /// </summary>
    /// <param name="value">The span to write.</param>
    public void AppendFormatted(ReadOnlySpan<char> value)
        => writer.Write(value);

    /// <inheritdoc />
    public readonly override string ToString() => writer.ToString();

    /// <summary>
    /// Releases the buffer associated with this handler.
    /// </summary>
    public void Dispose()
    {
        writer.Dispose();
        this = default;
    }
}
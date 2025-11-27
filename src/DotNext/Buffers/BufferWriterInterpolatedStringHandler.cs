using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

/// <summary>
/// Represents handler of the interpolated string
/// that can be written to <see cref="IBufferWriter{T}"/> without temporary allocations.
/// </summary>
[InterpolatedStringHandler]
[EditorBrowsable(EditorBrowsableState.Never)]
[StructLayout(LayoutKind.Auto)]
public struct BufferWriterInterpolatedStringHandler
{
    private readonly IBufferWriter<char> buffer;
    private readonly IFormatProvider? provider;
    private int count;

    /// <summary>
    /// Initializes a new interpolated string handler.
    /// </summary>
    /// <param name="literalLength">The total number of characters in known at compile-time.</param>
    /// <param name="formattedCount">The number of placeholders.</param>
    /// <param name="buffer">The output buffer.</param>
    /// <param name="provider">Optional formatting provider.</param>
    public BufferWriterInterpolatedStringHandler(int literalLength, int formattedCount, IBufferWriter<char> buffer, IFormatProvider? provider = null)
    {
        this.buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        this.provider = provider;

        // assume that every placeholder will be converted to substring no longer than X chars
        const int charsPerPlaceholder = 10;
        buffer.GetSpan((formattedCount * charsPerPlaceholder) + literalLength);
    }

    /// <summary>
    /// Gets number of written characters.
    /// </summary>
    public readonly int WrittenCount => count;

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
        => count += CharBuffer.AppendFormatted<T, BufferWriterReference<char>>(new(buffer), value, format, provider);

    /// <summary>
    /// Writes the specified character span to the handler.
    /// </summary>
    /// <param name="value">The span to write.</param>
    public void AppendFormatted(scoped ReadOnlySpan<char> value)
    {
        buffer.Write(value);
        count += value.Length;
    }

    /// <summary>
    /// Writes the specified string of chars to the handler.
    /// </summary>
    /// <param name="value">The span to write.</param>
    /// <param name="alignment">
    /// Minimum number of characters that should be written for this value. If the value is negative,
    /// it indicates left-aligned and the required minimum is the absolute value.
    /// </param>
    public void AppendFormatted(scoped ReadOnlySpan<char> value, int alignment)
        => count += CharBuffer.AppendFormatted<BufferWriterReference<char>>(new(buffer), value, alignment);

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
        => count += CharBuffer.AppendFormatted<T, BufferWriterReference<char>>(new(buffer), value, alignment, format, provider);
}
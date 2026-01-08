using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNext.Text;

using Buffers;

/// <summary>
/// Represents converter of interpolated string directly to a sequence of bytes
/// using the specified encoding.
/// </summary>
[InterpolatedStringHandler]
[EditorBrowsable(EditorBrowsableState.Advanced)]
[StructLayout(LayoutKind.Auto)]
public ref struct BufferWriterSlimEncodingInterpolatedStringHandler : IInterpolatedStringHandler
{
    private readonly Span<char> buffer;
    private readonly Encoder encoder;
    private readonly IFormatProvider? provider;
    private readonly BufferWriterSlim<byte>.Ref writer;
    private int count;
    
    /// <summary>
    /// Initializes a new interpolated string handler.
    /// </summary>
    /// <param name="literalLength">The total number of characters in known at compile-time.</param>
    /// <param name="formattedCount">The number of placeholders.</param>
    /// <param name="writer">The output buffer.</param>
    /// <param name="encoder">The character encoder.</param>
    /// <param name="buffer">The preallocated temporary buffer for characters.</param>
    /// <param name="provider">The format provider.</param>
    /// <exception cref="ArgumentNullException"><paramref name="encoder"/> is <see langword="null"/>.</exception>
    public BufferWriterSlimEncodingInterpolatedStringHandler(int literalLength,
        int formattedCount,
        ref BufferWriterSlim<byte> writer,
        Encoder encoder,
        Span<char> buffer = default,
        IFormatProvider? provider = null)
    {
        this.encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
        this.writer = new(ref writer);
        this.buffer = buffer;
        this.provider = provider;
        
        writer.InternalGetSpan(IInterpolatedStringHandler.EstimateUtf8BufferSize(literalLength, formattedCount));
    }

    /// <summary>
    /// Gets number of written characters.
    /// </summary>
    public readonly int WrittenCount => count;

    /// <summary>
    /// Writes the specified string to the handler.
    /// </summary>
    /// <param name="value">The string to write.</param>
    public void AppendLiteral(string? value) => AppendFormatted(value.AsSpan());

    /// <summary>
    /// Writes the specified value to the handler.
    /// </summary>
    /// <typeparam name="T">The type of the value to write.</typeparam>
    /// <param name="value">The value to write.</param>
    /// <param name="format">The format string.</param>
    public void AppendFormatted<T>(T value, string? format)
        => count += IInterpolatedStringHandler.AppendFormatted(
            writer,
            encoder,
            buffer,
            value,
            format,
            provider);

    /// <summary>
    /// Writes the specified character span to the handler.
    /// </summary>
    /// <param name="value">The span to write.</param>
    public void AppendFormatted(scoped ReadOnlySpan<char> value)
        => count += IInterpolatedStringHandler.AppendFormatted(writer, encoder, value);

    /// <summary>
    /// Writes the specified string of chars to the handler.
    /// </summary>
    /// <param name="value">The span to write.</param>
    /// <param name="alignment">
    /// Minimum number of characters that should be written for this value. If the value is negative,
    /// it indicates left-aligned and the required minimum is the absolute value.
    /// </param>
    public void AppendFormatted(scoped ReadOnlySpan<char> value, int alignment)
        => count += IInterpolatedStringHandler.AppendFormatted(writer, encoder, buffer, value, alignment);

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
        => count += IInterpolatedStringHandler.AppendFormatted(
            writer,
            encoder,
            buffer,
            value,
            alignment,
            format,
            provider);
}
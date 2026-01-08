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
public ref struct EncodingInterpolatedStringHandler : IInterpolatedStringHandler
{
    private readonly IFormatProvider? provider;
    private readonly Span<char> buffer;
    private readonly Encoder encoder;
    private InlineBufferWriter<byte> writer;
    
    /// <summary>
    /// Initializes a new interpolated string handler.
    /// </summary>
    /// <param name="literalLength">The total number of characters in known at compile-time.</param>
    /// <param name="formattedCount">The number of placeholders.</param>
    /// <param name="allocator">The allocator of the encoded buffer.</param>
    /// <param name="encoder">The character encoder.</param>
    /// <param name="buffer">The preallocated temporary buffer for characters.</param>
    /// <param name="provider">The format provider.</param>
    /// <exception cref="ArgumentNullException"><paramref name="encoder"/> is <see langword="null"/>.</exception>
    /// <exception cref="InsufficientMemoryException">The interpolated string template is too long.</exception>
    public EncodingInterpolatedStringHandler(int literalLength,
        int formattedCount,
        MemoryAllocator<byte>? allocator,
        Encoder encoder,
        Span<char> buffer = default,
        IFormatProvider? provider = null)
    {
        this.encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
        
        // assume that every placeholder will be converted to substring no longer than X chars
        var length = IInterpolatedStringHandler.EstimateUtf16BufferSize(literalLength, formattedCount);

        writer = (uint)length <= (uint)Array.MaxLength
            ? new(allocator) { Capacity = length }
            : throw new InsufficientMemoryException();

        this.buffer = buffer;
        this.provider = provider;
    }

    internal MemoryOwner<byte> DetachBuffer() => writer.DetachBuffer();
    
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
        => IInterpolatedStringHandler.AppendFormatted<T, InlineBufferWriter<byte>.Ref>(
            new(ref writer),
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
        => IInterpolatedStringHandler.AppendFormatted<InlineBufferWriter<byte>.Ref>(new(ref writer), encoder, value);

    /// <summary>
    /// Writes the specified string of chars to the handler.
    /// </summary>
    /// <param name="value">The span to write.</param>
    /// <param name="alignment">
    /// Minimum number of characters that should be written for this value. If the value is negative,
    /// it indicates left-aligned and the required minimum is the absolute value.
    /// </param>
    public void AppendFormatted(scoped ReadOnlySpan<char> value, int alignment)
        => IInterpolatedStringHandler.AppendFormatted<InlineBufferWriter<byte>.Ref>(
            new(ref writer),
            encoder,
            buffer,
            value,
            alignment);

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
        => IInterpolatedStringHandler.AppendFormatted<T, InlineBufferWriter<byte>.Ref>(
            new(ref writer),
            encoder,
            buffer,
            value,
            alignment,
            format,
            provider);
}
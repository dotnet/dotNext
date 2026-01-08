using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace DotNext.Text;

using Buffers;

/// <summary>
/// Provides factory methods to create interpolated strings efficiently.
/// </summary>
public static partial class StringInterpolation
{
    /// <summary>
    /// Formats interpolated string as a rented buffer of characters.
    /// </summary>
    /// <param name="allocator">The allocator of the interpolated string.</param>
    /// <param name="encoder">The encoding context.</param>
    /// <param name="buffer">The preallocated buffer to be used for placing characters during encoding.</param>
    /// <param name="provider">Optional formatting provider to be applied for each interpolated string argument.</param>
    /// <param name="handler">The interpolated string handler.</param>
    /// <returns>A buffer containing formatted string.</returns>
    public static MemoryOwner<byte> Interpolate(MemoryAllocator<byte>? allocator, Encoder encoder, Span<char> buffer, IFormatProvider? provider,
        [InterpolatedStringHandlerArgument(nameof(allocator), nameof(encoder), nameof(buffer), nameof(provider))]
        scoped ref EncodingInterpolatedStringHandler handler)
        => handler.DetachBuffer();

    /// <summary>
    /// Formats interpolated string as a rented buffer of characters.
    /// </summary>
    /// <param name="allocator">The allocator of the interpolated string.</param>
    /// <param name="encoder">The encoding context.</param>
    /// <param name="buffer">The preallocated buffer to be used for placing characters during encoding.</param>
    /// <param name="handler">The interpolated string handler.</param>
    /// <returns>A buffer containing formatted string.</returns>
    public static MemoryOwner<byte> Interpolate(MemoryAllocator<byte>? allocator, Encoder encoder, Span<char> buffer,
        [InterpolatedStringHandlerArgument(nameof(allocator), nameof(encoder), nameof(buffer))]
        scoped ref EncodingInterpolatedStringHandler handler)
        => Interpolate(allocator, encoder, buffer, provider: null, ref handler);

    /// <summary>
    /// Encodes formattable string as a sequence of bytes.
    /// </summary>
    /// <param name="writer">The output buffer.</param>
    /// <param name="encoder">The encoding context.</param>
    /// <param name="buffer">The preallocated buffer to be used for placing characters during encoding.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="handler">The interpolated string handler.</param>
    /// <returns>The number of produced bytes.</returns>
    public static int Interpolate(this IBufferWriter<byte> writer, Encoder encoder, Span<char> buffer, IFormatProvider? provider,
        [InterpolatedStringHandlerArgument(nameof(writer), nameof(encoder), nameof(buffer), nameof(provider))]
        in BufferWriterEncodingInterpolatedStringHandler handler)
        => handler.WrittenCount;

    /// <summary>
    /// Encodes formattable string as a sequence of bytes.
    /// </summary>
    /// <param name="writer">The output buffer.</param>
    /// <param name="encoder">The encoding context.</param>
    /// <param name="buffer">The preallocated buffer to be used for placing characters during encoding.</param>
    /// <param name="handler">The interpolated string handler.</param>
    /// <returns>The number of produced bytes.</returns>
    public static int Interpolate(this IBufferWriter<byte> writer, Encoder encoder, Span<char> buffer,
        [InterpolatedStringHandlerArgument(nameof(writer), nameof(encoder), nameof(buffer))] in BufferWriterEncodingInterpolatedStringHandler handler)
        => Interpolate(writer, encoder, buffer, provider: null, in handler);

    /// <summary>
    /// Encodes formattable string as a sequence of bytes.
    /// </summary>
    /// <param name="writer">The output buffer.</param>
    /// <param name="encoder">The encoding context.</param>
    /// <param name="buffer">The preallocated buffer to be used for placing characters during encoding.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="handler">The interpolated string handler.</param>
    /// <returns>The number of produced bytes.</returns>
    public static int Interpolate(this ref BufferWriterSlim<byte> writer, Encoder encoder, scoped Span<char> buffer, IFormatProvider? provider,
        [InterpolatedStringHandlerArgument(nameof(writer), nameof(encoder), nameof(buffer), nameof(provider))] scoped in BufferWriterSlimEncodingInterpolatedStringHandler handler)
        => handler.WrittenCount;

    /// <summary>
    /// Encodes formattable string as a sequence of bytes.
    /// </summary>
    /// <param name="writer">The output buffer.</param>
    /// <param name="encoder">The encoding context.</param>
    /// <param name="buffer">The preallocated buffer to be used for placing characters during encoding.</param>
    /// <param name="handler">The interpolated string handler.</param>
    /// <returns>The number of produced bytes.</returns>
    public static int Interpolate(this ref BufferWriterSlim<byte> writer, Encoder encoder, scoped Span<char> buffer,
        [InterpolatedStringHandlerArgument(nameof(writer), nameof(encoder), nameof(buffer))]
        scoped in BufferWriterSlimEncodingInterpolatedStringHandler handler)
        => handler.WrittenCount;
}
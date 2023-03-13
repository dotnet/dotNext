using System.Buffers;
using System.Runtime.CompilerServices;

namespace DotNext.Buffers;

using EncodingContext = DotNext.Text.EncodingContext;

public partial class BufferWriter
{
    /// <summary>
    /// Encodes formattable string as a sequence of bytes.
    /// </summary>
    /// <param name="writer">The output buffer.</param>
    /// <param name="context">The encoding context.</param>
    /// <param name="buffer">The preallocated buffer to be used for placing characters during encoding.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="handler">The interpolated string handler.</param>
    /// <returns>The number of produced bytes.</returns>
    public static int WriteString(this IBufferWriter<byte> writer, in EncodingContext context, Span<char> buffer, IFormatProvider? provider, [InterpolatedStringHandlerArgument(nameof(writer), nameof(context), nameof(buffer), nameof(provider))] ref EncodingInterpolatedStringHandler handler)
        => handler.WrittenCount;

    /// <summary>
    /// Encodes formattable string as a sequence of bytes.
    /// </summary>
    /// <param name="writer">The output buffer.</param>
    /// <param name="context">The encoding context.</param>
    /// <param name="handler">The interpolated string handler.</param>
    /// <returns>The number of produced bytes.</returns>
    public static int WriteString(this IBufferWriter<byte> writer, in EncodingContext context, [InterpolatedStringHandlerArgument(nameof(writer), nameof(context))] ref EncodingInterpolatedStringHandler handler)
        => WriteString(writer, in context, Span<char>.Empty, null, ref handler);
}
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers.Text;

using static Runtime.Intrinsics;

/// <summary>
/// Represents base64 encoder suitable for encoding large binary
/// data using streaming approach.
/// </summary>
/// <remarks>
/// This type maintains internal state for correct encoding of streaming data.
/// Therefore, it must be passed by reference to any routine. It's not a <c>ref struct</c>
/// to allow construction of high-level encoders in the form of classes.
/// The output can be in the form of UTF-8 encoded bytes or Unicode characters.
/// Encoding methods should not be intermixed by the caller code.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
[DebuggerDisplay($"BufferedData = {{{nameof(BufferedDataString)}}}")]
public partial struct Base64Encoder : IResettable
{
    /// <summary>
    /// Gets the maximum number of bytes that can be buffered by the encoder.
    /// </summary>
    public const int MaxBufferedDataSize = sizeof(ushort);

    /// <summary>
    /// Gets the maximum number of characters that can be produced by <see cref="Flush(Span{byte})"/>
    /// or <see cref="Flush(Span{char})"/> methods.
    /// </summary>
    public const int MaxCharsToFlush = (MaxBufferedDataSize + 2) / 3 * 4;

    /// <summary>
    /// Gets the maximum size of the input block of bytes to encode.
    /// </summary>
    public const int MaxInputSize = int.MaxValue / 4 * 3;

    private const int DecodingBufferSize = 258;

    private const int EncodingBufferSize = (DecodingBufferSize + 2) / 3 * 4;

    // 2 bytes reserved if the input is not a multiple of 3
    private ushort reservedBuffer;

    // possible values are 0, 1 or 2
    private int reservedBufferSize;

    /// <summary>
    /// Indicates that the size of the encoded data is not a multiple of 3
    /// and this encoder expects input data.
    /// </summary>
    public readonly bool HasBufferedData => reservedBufferSize > 0;

    /// <summary>
    /// Gets the buffered data.
    /// </summary>
    /// <remarks>
    /// The length of returned span is in [0..<see cref="MaxBufferedDataSize"/>] range.
    /// </remarks>
    [UnscopedRef]
    public readonly ReadOnlySpan<byte> BufferedData
    {
        get
        {
            Debug.Assert((uint)reservedBufferSize <= sizeof(ushort));

            return MemoryMarshal.CreateReadOnlySpan(in InToRef<ushort, byte>(in reservedBuffer), reservedBufferSize);
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    [UnscopedRef]
    private Span<byte> Buffer => Span.AsBytes(ref reservedBuffer);

    [ExcludeFromCodeCoverage]
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly string BufferedDataString
    {
        get
        {
            var bufferedData = BufferedData;
            return bufferedData.IsEmpty ? string.Empty : Convert.ToBase64String(bufferedData);
        }
    }

    /// <summary>
    /// Resets the internal state of the encoder.
    /// </summary>
    public void Reset() => reservedBufferSize = 0;
}
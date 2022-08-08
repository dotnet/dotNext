using System.Runtime.InteropServices;

namespace DotNext.Text;

using NewBase64Encoder = Buffers.Text.Base64Encoder;

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
[Obsolete("Use DotNext.Buffers.Text.Base64Encoder type instead.")]
public partial struct Base64Encoder
{
    /// <summary>
    /// Gets the maximum number of bytes that can be buffered by the encoder.
    /// </summary>
    public const int MaxBufferedDataSize = NewBase64Encoder.MaxBufferedDataSize;

    /// <summary>
    /// Gets the maximum number of characters that can be produced by <see cref="Flush(Span{byte})"/>
    /// or <see cref="Flush(Span{char})"/> methods.
    /// </summary>
    public const int MaxCharsToFlush = NewBase64Encoder.MaxCharsToFlush;

    /// <summary>
    /// Gets the maximum size of the input block of bytes to encode.
    /// </summary>
    public const int MaxInputSize = NewBase64Encoder.MaxInputSize;

    private NewBase64Encoder encoder;

    /// <summary>
    /// Indicates that the size of the encoded data is not a multiple of 3
    /// and the encoder.
    /// </summary>
    public readonly bool HasBufferedData => encoder.HasBufferedData;

    /// <summary>
    /// Gets the number of buffered bytes.
    /// </summary>
    /// <remarks>
    /// The range of the returned value is [0..<see cref="MaxBufferedDataSize"/>].
    /// </remarks>
    public readonly int BufferedDataSize => encoder.BufferedDataSize;

    /// <summary>
    /// Gets the buffered data.
    /// </summary>
    /// <param name="output">The output buffer.</param>
    /// <returns>The number of bytes copied to <paramref name="output"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="output"/> is not large enough.</exception>
    public readonly int GetBufferedData(Span<byte> output)
        => encoder.GetBufferedData(output);

    /// <summary>
    /// Resets the internal state of the encoder.
    /// </summary>
    public void Reset() => encoder.Reset();
}
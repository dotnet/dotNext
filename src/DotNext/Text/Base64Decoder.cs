using System.Runtime.InteropServices;

namespace DotNext.Text;

using NewBase64Decoder = Buffers.Text.Base64Decoder;

/// <summary>
/// Represents base64 decoder suitable for decoding large base64-encoded binary
/// data using streaming approach.
/// </summary>
/// <remarks>
/// This type maintains internal state for correct decoding of streaming data.
/// Therefore, it must be passed by reference to any routine. It's not a <c>ref struct</c>
/// to allow construction of high-level decoders in the form of classes.
/// Base64-encoded bytes can be accepted as UTF-8 or Unicode characters.
/// Decoding methods should not be intermixed by the caller code.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
[Obsolete("Use DotNext.Buffers.Text.Base64Decoder type instead.")]
public partial struct Base64Decoder
{
    private NewBase64Decoder decoder;

    /// <summary>
    /// Indicates that decoders expected additional data to decode.
    /// </summary>
    public readonly bool NeedMoreData => decoder.NeedMoreData;

    /// <summary>
    /// Resets the internal state of the decoder.
    /// </summary>
    public void Reset() => decoder.Reset();
}
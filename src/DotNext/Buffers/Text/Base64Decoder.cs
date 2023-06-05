using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotNext.Buffers.Text;

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
[DebuggerDisplay($"NeedMoreData = {{{nameof(NeedMoreData)}}}")]
public partial struct Base64Decoder : IResettable
{
    private const int DecodingBufferSize = 258;
    private const int GotPaddingFlag = -1;

    // 8 bytes buffer for decoding base64
    // for utf8 encoding we need just 4 bytes
    // but for Unicode we need 8 bytes, because max chars in reserve is 4 (4 X sizeof(char) == 8 bytes)
    private ulong reservedBuffer;
    private int reservedBufferSize; // negative if EOS reached

    /// <summary>
    /// Indicates that the decoder expects additional data to decode.
    /// </summary>
    public readonly bool NeedMoreData => reservedBufferSize > 0;

    /// <summary>
    /// Resets the internal state of the decoder.
    /// </summary>
    public void Reset() => reservedBufferSize = 0;
}
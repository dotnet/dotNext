using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotNext.Buffers.Text;

using Buffers;

public partial struct Base64Decoder
{
    private const byte PaddingByte = (byte)PaddingChar;

    private bool DecodeFromUtf8Core(scoped ReadOnlySpan<byte> chars, ref BufferWriterSlim<byte> bytes)
    {
        Debug.Assert(reservedBufferSize >= 0);

        var produced = Base64.GetMaxDecodedFromUtf8Length(chars.Length);
        var buffer = bytes.InternalGetSpan(produced);

        // x & 3 is the same as x % 4
        switch (Base64.DecodeFromUtf8(chars, buffer, out var consumed, out produced, (chars.Length & 3) is 0))
        {
            default:
                return false;
            case OperationStatus.Done:
                reservedBufferSize = chars is [.., PaddingByte] ? GotPaddingFlag : 0;
                break;
            case OperationStatus.DestinationTooSmall:
                reservedBufferSize = 0;
                break;
            case OperationStatus.NeedMoreData:
                reservedBufferSize = chars.Length - consumed;
                Debug.Assert(reservedBufferSize <= MaxBufferedChars);
                chars.Slice(consumed).CopyTo(ByteBuffer);
                break;
        }

        bytes.Advance(produced);
        return true;
    }

    private bool DecodeFromUtf8Buffered(scoped ReadOnlySpan<byte> chars, ref BufferWriterSlim<byte> bytes)
    {
        if (NeedMoreData)
        {
            var tempBuffer = new SpanWriter<byte>(stackalloc byte[MaxBufferedChars + 1]);
            tempBuffer.Write(BufferedBytes);
            chars = chars.Slice(tempBuffer.Write(chars));

            if (!DecodeFromUtf8Core(chars, ref bytes))
                return false;
        }

        return chars.IsEmpty || DecodeFromUtf8Core(chars, ref bytes);
    }

    /// <summary>
    /// Decodes UTF-8 encoded base64 string.
    /// </summary>
    /// <param name="chars">UTF-8 encoded portion of base64 string.</param>
    /// <param name="bytes">The output growable buffer used to write decoded bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    public void DecodeFromUtf8(ReadOnlySpan<byte> chars, IBufferWriter<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (reservedBufferSize is GotPaddingFlag)
            goto bad_data;

        var maxBytes = GetMaxDecodedLength(chars.Length);
        var writer = new BufferWriterSlim<byte>(bytes.GetSpan(maxBytes));
        if (!DecodeFromUtf8Buffered(chars, ref writer))
            goto bad_data;

        Debug.Assert(writer.WrittenCount <= maxBytes);
        bytes.Advance(writer.WrittenCount);
        return;

    bad_data:
        throw new FormatException(ExceptionMessages.MalformedBase64);
    }

    /// <summary>
    /// Decoes UTF-8 encoded base64 string.
    /// </summary>
    /// <param name="chars">UTF-8 encoded portion of base64 string.</param>
    /// <param name="allocator">The allocator of the result buffer.</param>
    /// <returns>A buffer containing decoded bytes.</returns>
    public MemoryOwner<byte> DecodeFromUtf8(ReadOnlySpan<byte> chars, MemoryAllocator<byte>? allocator = null)
    {
        if (reservedBufferSize is GotPaddingFlag)
            goto bad_data;

        var bytes = new BufferWriterSlim<byte>(GetMaxDecodedLength(chars.Length), allocator);
        if (!DecodeFromUtf8Buffered(chars, ref bytes))
        {
            bytes.Dispose();
            goto bad_data;
        }

        if (!bytes.TryDetachBuffer(out var result))
        {
            result = bytes.WrittenSpan.Copy(allocator);
            bytes.Dispose();
        }

        return result;

    bad_data:
        throw new FormatException(ExceptionMessages.MalformedBase64);
    }

    /// <summary>
    /// Decodes UTF-8 encoded base64 string.
    /// </summary>
    /// <param name="chars">UTF-8 encoded portion of base64 string.</param>
    /// <param name="bytes">The output growable buffer used to write decoded bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    public void DecodeFromUtf8(ReadOnlySpan<byte> chars, ref BufferWriterSlim<byte> bytes)
    {
        if (reservedBufferSize is GotPaddingFlag || !DecodeFromUtf8Buffered(chars, ref bytes))
            throw new FormatException(ExceptionMessages.MalformedBase64);
    }

    /// <summary>
    /// Decodes a sequence of base64-encoded bytes.
    /// </summary>
    /// <param name="utf8Chars">A sequence of base64-encoded bytes.</param>
    /// <param name="allocator">The allocator of the buffer used for decoded bytes.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>A sequence of decoded bytes.</returns>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async IAsyncEnumerable<ReadOnlyMemory<byte>> DecodeFromUtf8Async(IAsyncEnumerable<ReadOnlyMemory<byte>> utf8Chars, MemoryAllocator<byte>? allocator = null, [EnumeratorCancellation] CancellationToken token = default)
    {
        var decoder = new Base64Decoder();
        MemoryOwner<byte> buffer;

        await foreach (var chunk in utf8Chars.WithCancellation(token).ConfigureAwait(false))
        {
            using (buffer = decoder.DecodeFromUtf8(chunk.Span, allocator))
                yield return buffer.Memory;
        }

        if (decoder.NeedMoreData)
            throw new FormatException(ExceptionMessages.MalformedBase64);
    }
}
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;

namespace DotNext.Buffers.Text;

using Buffers;

public partial struct Base64Decoder
{
    private const byte PaddingByte = (byte)PaddingChar;

    private bool DecodeFromUtf8Core<TWriter>(scoped ReadOnlySpan<byte> chars, scoped TWriter bytes)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        Debug.Assert(reservedBufferSize >= 0);

        var produced = Base64.GetMaxDecodedFromUtf8Length(chars.Length);
        scoped var buffer = bytes.GetSpan(produced);

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

    private bool DecodeFromUtf8Buffered<TWriter>(scoped ReadOnlySpan<byte> chars, scoped TWriter bytes)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        if (NeedMoreData)
        {
            var tempBuffer = new SpanWriter<byte>(stackalloc byte[MaxBufferedChars + 1]);
            tempBuffer.Write(BufferedBytes);
            chars = chars.Slice(tempBuffer.Write(chars));

            if (!DecodeFromUtf8Core(chars, bytes))
                return false;
        }

        return chars.IsEmpty || DecodeFromUtf8Core(chars, bytes);
    }

    /// <summary>
    /// Decodes UTF-8 encoded base64 string.
    /// </summary>
    /// <param name="chars">UTF-8 encoded portion of base64 string.</param>
    /// <param name="bytes">The output growable buffer used to write decoded bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    public void DecodeFromUtf8(scoped ReadOnlySpan<byte> chars, IBufferWriter<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        DecodeFromUtf8<BufferWriterReference<byte>>(chars, new(bytes));
    }

    /// <summary>
    /// Decodes UTF-8 encoded base64 string.
    /// </summary>
    /// <param name="chars">UTF-8 encoded portion of base64 string.</param>
    /// <param name="allocator">The allocator of the result buffer.</param>
    /// <returns>A buffer containing decoded bytes.</returns>
    public MemoryOwner<byte> DecodeFromUtf8(scoped ReadOnlySpan<byte> chars, MemoryAllocator<byte>? allocator = null)
    {
        if (reservedBufferSize is GotPaddingFlag)
            goto bad_data;

        var bytes = new BufferWriterSlim<byte>(GetMaxDecodedLength(chars.Length), allocator);
        if (DecodeFromUtf8Buffered<BufferWriterSlim<byte>.Ref>(chars, new(ref bytes)))
            return bytes.DetachOrCopyBuffer();

        bytes.Dispose();

        bad_data:
        throw CreateFormatException();
    }

    /// <inheritdoc/>
    MemoryOwner<byte> IBufferedDecoder<byte>.Decode(ReadOnlySpan<byte> chars, MemoryAllocator<byte>? allocator) => DecodeFromUtf8(chars, allocator);

    /// <summary>
    /// Decodes UTF-8 encoded base64 string.
    /// </summary>
    /// <param name="chars">UTF-8 encoded portion of base64 string.</param>
    /// <param name="bytes">The output growable buffer used to write decoded bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    public void DecodeFromUtf8(scoped ReadOnlySpan<byte> chars, scoped ref BufferWriterSlim<byte> bytes)
        => DecodeFromUtf8<BufferWriterSlim<byte>.Ref>(chars, new(ref bytes));

    private void DecodeFromUtf8<TWriter>(scoped ReadOnlySpan<byte> chars, scoped TWriter writer)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        if (reservedBufferSize is GotPaddingFlag || !DecodeFromUtf8Buffered(chars, writer))
            throw CreateFormatException();
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
    public static IAsyncEnumerable<ReadOnlyMemory<byte>> DecodeFromUtf8Async(IAsyncEnumerable<ReadOnlyMemory<byte>> utf8Chars,
        MemoryAllocator<byte>? allocator = null, CancellationToken token = default)
        => IBufferedDecoder<byte>.DecodeAsync<Base64Decoder>(utf8Chars, allocator, token);
}
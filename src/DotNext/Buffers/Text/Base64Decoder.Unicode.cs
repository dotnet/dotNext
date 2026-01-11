using System.Buffers;
using System.Diagnostics;

namespace DotNext.Buffers.Text;

using Buffers;

public partial struct Base64Decoder
{
    private const char PaddingChar = '=';

    private bool DecodeFromUtf16Core<TWriter>(scoped ReadOnlySpan<char> chars, scoped TWriter writer)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        Debug.Assert(reservedBufferSize >= 0);

        var size = chars.Length & 3;
        if (size is not 0)
        {
            // size of the rest
            size = chars.Length - size;
            var rest = chars.Slice(size);
            rest.CopyTo(CharBuffer);
            reservedBufferSize = rest.Length; // keep the number of chars, not bytes
            chars = chars.Slice(0, size);
        }
        else if (chars is [.., PaddingChar])
        {
            reservedBufferSize = GotPaddingFlag;
        }
        else
        {
            reservedBufferSize = 0;
        }

        bool result;

        // 4 characters => 3 bytes
        if (result = Convert.TryFromBase64Chars(chars, writer.GetSpan(chars.Length), out size))
            writer.Advance(size);

        return result;
    }

    private bool DecodeFromUtf16Buffered<TWriter>(scoped ReadOnlySpan<char> chars, scoped TWriter writer)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        if (NeedMoreData)
        {
            var tempBuffer = new SpanWriter<char>(stackalloc char[MaxBufferedChars + 1]);
            tempBuffer.Write(BufferedChars);
            chars = chars.Slice(tempBuffer.Write(chars));

            if (!DecodeFromUtf16Core(chars, writer))
                return false;
        }

        return chars.IsEmpty || DecodeFromUtf16Core(chars, writer);
    }

    /// <summary>
    /// Decodes base64 characters.
    /// </summary>
    /// <param name="chars">The span containing base64-encoded bytes.</param>
    /// <param name="bytes">The output growable buffer used to write decoded bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    public void DecodeFromUtf16(scoped ReadOnlySpan<char> chars, IBufferWriter<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        DecodeFromUtf16<BufferWriterReference<byte>>(chars, new(bytes));
    }

    /// <summary>
    /// Decodes base64 characters.
    /// </summary>
    /// <param name="chars">The span containing base64-encoded bytes.</param>
    /// <param name="allocator">The allocator of the result buffer.</param>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    /// <returns>A buffer containing decoded bytes.</returns>
    public MemoryOwner<byte> DecodeFromUtf16(scoped ReadOnlySpan<char> chars, MemoryAllocator<byte>? allocator = null)
    {
        if (reservedBufferSize is GotPaddingFlag)
            goto bad_data;

        var bytes = new BufferWriterSlim<byte>(GetMaxDecodedLength(chars.Length), allocator);
        if (DecodeFromUtf16Buffered<BufferWriterSlim<byte>.Ref>(chars, new(ref bytes)))
            return bytes.DetachOrCopyBuffer();

        bytes.Dispose();

        bad_data:
        throw CreateFormatException();
    }

    /// <inheritdoc/>
    MemoryOwner<byte> IBufferedDecoder<char>.Decode(ReadOnlySpan<char> chars, MemoryAllocator<byte>? allocator) => DecodeFromUtf16(chars, allocator);

    /// <summary>
    /// Decodes UTF-8 encoded base64 string.
    /// </summary>
    /// <param name="chars">UTF-8 encoded portion of base64 string.</param>
    /// <param name="bytes">The output growable buffer used to write decoded bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    public void DecodeFromUtf16(scoped ReadOnlySpan<char> chars, scoped ref BufferWriterSlim<byte> bytes)
        => DecodeFromUtf16<BufferWriterSlim<byte>.Ref>(chars, new(ref bytes));

    private void DecodeFromUtf16<TWriter>(ReadOnlySpan<char> chars, TWriter writer)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        if (reservedBufferSize is GotPaddingFlag || !DecodeFromUtf16Buffered(chars, writer))
            throw CreateFormatException();
    }

    /// <summary>
    /// Decodes a sequence of base64-encoded bytes.
    /// </summary>
    /// <param name="chars">A sequence of base64-encoded bytes.</param>
    /// <param name="allocator">The allocator of the buffer used for decoded bytes.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>A sequence of decoded bytes.</returns>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static IAsyncEnumerable<ReadOnlyMemory<byte>> DecodeFromUtf16Async(IAsyncEnumerable<ReadOnlyMemory<char>> chars,
        MemoryAllocator<byte>? allocator = null, CancellationToken token = default)
        => IBufferedDecoder<char>.DecodeAsync<Base64Decoder>(chars, allocator, token);
}
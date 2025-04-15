using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Text.Unicode;

namespace DotNext.Buffers.Text;

using Buffers;

public partial struct Base64Encoder
{
    private void EncodeToUtf16Core(scoped ReadOnlySpan<byte> bytes, ref BufferWriterSlim<char> chars, bool flush)
    {
        var size = bytes.Length % 3;

        if (size is 0 || flush)
        {
            Reset();
        }
        else
        {
            // size of the rest
            size = bytes.Length - size;
            var rest = bytes.Slice(size);
            rest.CopyTo(Buffer);
            reservedBufferSize = rest.Length;
            bytes = bytes.Slice(0, size);
        }

        Convert.TryToBase64Chars(bytes, chars.InternalGetSpan(Base64.GetMaxEncodedToUtf8Length(bytes.Length)), out size);
        chars.Advance(size);
    }

    private void EncodeToUtf16Buffered(scoped ReadOnlySpan<byte> bytes, ref BufferWriterSlim<char> chars, bool flush)
    {
        if (HasBufferedData)
        {
            var tempBuffer = new SpanWriter<byte>(stackalloc byte[MaxBufferedDataSize + 1]);
            tempBuffer.Write(BufferedData);
            bytes = bytes.Slice(tempBuffer.Write(bytes));

            EncodeToUtf16Core(tempBuffer.WrittenSpan, ref chars, bytes.IsEmpty && flush);
        }

        if (bytes.IsEmpty is false)
        {
            EncodeToUtf16Core(bytes, ref chars, flush);
        }
    }

    /// <summary>
    /// Encodes a block of bytes to base64-encoded characters.
    /// </summary>
    /// <param name="bytes">A block of bytes to encode.</param>
    /// <param name="allocator">The allocator of the result buffer.</param>
    /// <param name="flush">
    /// <see langword="true"/> to encode the final block and insert padding if necessary;
    /// <see langword="false"/> to encode a fragment without padding.
    /// </param>
    /// <returns>The buffer containing encoded bytes.</returns>
    /// <exception cref="ArgumentException">The length of <paramref name="bytes"/> is greater than <see cref="MaxInputSize"/>.</exception>
    public MemoryOwner<char> EncodeToUtf16(ReadOnlySpan<byte> bytes, MemoryAllocator<char>? allocator = null, bool flush = false)
    {
        if (bytes.Length >= MaxInputSize)
            throw new ArgumentException(ExceptionMessages.LargeBuffer, nameof(bytes));

        var writer = new BufferWriterSlim<char>(GetMaxEncodedLength(bytes.Length), allocator);
        EncodeToUtf16Buffered(bytes, ref writer, flush);

        return writer.DetachOrCopyBuffer();
    }

    /// <inheritdoc/>
    MemoryOwner<char> IBufferedEncoder<char>.Encode(ReadOnlySpan<byte> bytes, MemoryAllocator<char>? allocator) => EncodeToUtf16(bytes, allocator);

    /// <summary>
    /// Encodes a block of bytes to base64-encoded characters.
    /// </summary>
    /// <param name="bytes">A block of bytes to encode.</param>
    /// <param name="chars">The buffer of characters to write into.</param>
    /// <param name="flush">
    /// <see langword="true"/> to encode the final block and insert padding if necessary;
    /// <see langword="false"/> to encode a fragment without padding.
    /// </param>
    /// <exception cref="ArgumentException">The length of <paramref name="bytes"/> is greater than <see cref="MaxInputSize"/>.</exception>
    public void EncodeToUtf16(ReadOnlySpan<byte> bytes, ref BufferWriterSlim<char> chars, bool flush = false)
    {
        if (bytes.Length > MaxInputSize)
            throw new ArgumentException(ExceptionMessages.LargeBuffer, nameof(bytes));

        EncodeToUtf16Buffered(bytes, ref chars, flush);
    }

    /// <summary>
    /// Encodes a block of bytes to base64-encoded characters.
    /// </summary>
    /// <param name="bytes">A block of bytes to encode.</param>
    /// <param name="chars">The output buffer.</param>
    /// <param name="flush">
    /// <see langword="true"/> to encode the final block and insert padding if necessary;
    /// <see langword="false"/> to encode a fragment without padding.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="chars"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The length of <paramref name="bytes"/> is greater than <see cref="MaxInputSize"/>.</exception>
    public void EncodeToUtf16(ReadOnlySpan<byte> bytes, IBufferWriter<char> chars, bool flush = false)
    {
        if (bytes.Length > MaxInputSize)
            throw new ArgumentException(ExceptionMessages.LargeBuffer, nameof(bytes));

        var maxChars = GetMaxEncodedLength(bytes.Length);
        var writer = new BufferWriterSlim<char>(chars.GetSpan(maxChars));
        EncodeToUtf16Buffered(bytes, ref writer, flush);

        Debug.Assert(writer.WrittenCount <= maxChars);
        chars.Advance(writer.WrittenCount);
    }

    /// <summary>
    /// Encodes a sequence of bytes to characters using base64 encoding.
    /// </summary>
    /// <param name="bytes">A collection of buffers.</param>
    /// <param name="allocator">Characters buffer allocator.</param>
    /// <param name="token">The token that can be used to cancel the encoding.</param>
    /// <returns>A collection of encoded bytes.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static IAsyncEnumerable<ReadOnlyMemory<char>> EncodeToUtf16Async(IAsyncEnumerable<ReadOnlyMemory<byte>> bytes,
        MemoryAllocator<char>? allocator = null, CancellationToken token = default)
        => IBufferedEncoder<char>.EncodeAsync<Base64Encoder>(bytes, allocator, token);

    /// <summary>
    /// Flushes the buffered data as base64-encoded characters to the output buffer.
    /// </summary>
    /// <param name="output">The buffer of characters.</param>
    /// <returns>The number of written characters.</returns>
    public int Flush(Span<char> output)
    {
        int charsWritten;

        if (reservedBufferSize is 0 || output.IsEmpty)
        {
            charsWritten = 0;
        }
        else
        {
            Span<byte> utf8Chars = stackalloc byte[MaxCharsToFlush];
            charsWritten = Flush(utf8Chars);
            Utf8.ToUtf16(utf8Chars.Slice(0, charsWritten), output, out _, out charsWritten);
        }

        return charsWritten;
    }
}
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotNext.Buffers.Text;

using Buffers;

public partial struct Base64Encoder
{
    private void EncodeToUtf8Core(scoped ReadOnlySpan<byte> bytes, ref BufferWriterSlim<byte> chars, bool flush)
    {
        var charsWritten = Base64.GetMaxEncodedToUtf8Length(bytes.Length);
        var buffer = chars.InternalGetSpan(charsWritten);

        switch (Base64.EncodeToUtf8(bytes, buffer, out var bytesRead, out charsWritten, (bytes.Length % 3) is 0 || flush))
        {
            case OperationStatus.DestinationTooSmall or OperationStatus.Done:
                Reset();
                break;
            case OperationStatus.NeedMoreData:
                reservedBufferSize = bytes.Length - bytesRead;
                Debug.Assert(reservedBufferSize <= MaxBufferedDataSize);
                bytes.Slice(bytesRead).CopyTo(Span.AsBytes(ref reservedBuffer));
                break;
        }

        chars.Advance(charsWritten);
    }

    private void EncodeToUtf8Buffered(scoped ReadOnlySpan<byte> bytes, ref BufferWriterSlim<byte> chars, bool flush)
    {
        if (HasBufferedData)
        {
            var tempBuffer = new SpanWriter<byte>(stackalloc byte[MaxBufferedDataSize + 1]);
            tempBuffer.Write(BufferedData);
            bytes = bytes.Slice(tempBuffer.Write(bytes));

            EncodeToUtf8Core(tempBuffer.WrittenSpan, ref chars, bytes.IsEmpty && flush);
        }

        if (bytes.IsEmpty is false)
        {
            EncodeToUtf8Core(bytes, ref chars, flush);
        }
    }

    /// <summary>
    /// Encodes a block of bytes to base64-encoded UTF-8 characters.
    /// </summary>
    /// <param name="bytes">A block of bytes to encode.</param>
    /// <param name="allocator">The allocator of the result buffer.</param>
    /// <param name="flush">
    /// <see langword="true"/> to encode the final block and insert padding if necessary;
    /// <see langword="false"/> to encode a fragment without padding.
    /// </param>
    /// <returns>The buffer containing encoded bytes.</returns>
    /// <exception cref="ArgumentException">The length of <paramref name="bytes"/> is greater than <see cref="MaxInputSize"/>.</exception>
    public MemoryOwner<byte> EncodeToUtf8(ReadOnlySpan<byte> bytes, MemoryAllocator<byte>? allocator = null, bool flush = false)
    {
        if (bytes.Length > MaxInputSize)
            throw new ArgumentException(ExceptionMessages.LargeBuffer, nameof(bytes));

        var writer = new BufferWriterSlim<byte>(GetMaxEncodedLength(bytes.Length), allocator);
        EncodeToUtf8Buffered(bytes, ref writer, flush);

        return writer.DetachOrCopyBuffer();
    }

    /// <summary>
    /// Encodes a block of bytes to base64-encoded UTF-8 characters.
    /// </summary>
    /// <param name="bytes">A block of bytes to encode.</param>
    /// <param name="chars">The buffer of UTF-8 characters to write into.</param>
    /// <param name="flush">
    /// <see langword="true"/> to encode the final block and insert padding if necessary;
    /// <see langword="false"/> to encode a fragment without padding.
    /// </param>
    /// <exception cref="ArgumentException">The length of <paramref name="bytes"/> is greater than <see cref="MaxInputSize"/>.</exception>
    public void EncodeToUtf8(ReadOnlySpan<byte> bytes, ref BufferWriterSlim<byte> chars, bool flush = false)
    {
        if (bytes.Length > MaxInputSize)
            throw new ArgumentException(ExceptionMessages.LargeBuffer, nameof(bytes));

        EncodeToUtf8Buffered(bytes, ref chars, flush);
    }

    /// <summary>
    /// Encodes a block of bytes to base64-encoded UTF-8 characters.
    /// </summary>
    /// <param name="bytes">A block of bytes to encode.</param>
    /// <param name="chars">The output buffer.</param>
    /// <param name="flush">
    /// <see langword="true"/> to encode the final block and insert padding if necessary;
    /// <see langword="false"/> to encode a fragment without padding.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="chars"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The length of <paramref name="bytes"/> is greater than <see cref="MaxInputSize"/>.</exception>
    public void EncodeToUtf8(ReadOnlySpan<byte> bytes, IBufferWriter<byte> chars, bool flush = false)
    {
        ArgumentNullException.ThrowIfNull(chars);

        if (bytes.Length > MaxInputSize)
            throw new ArgumentException(ExceptionMessages.LargeBuffer, nameof(bytes));

        var maxChars = GetMaxEncodedLength(bytes.Length);
        var writer = new BufferWriterSlim<byte>(chars.GetSpan(maxChars));
        EncodeToUtf8Buffered(bytes, ref writer, flush);

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
    public static async IAsyncEnumerable<ReadOnlyMemory<byte>> EncodeToUtf8Async(IAsyncEnumerable<ReadOnlyMemory<byte>> bytes, MemoryAllocator<byte>? allocator = null, [EnumeratorCancellation] CancellationToken token = default)
    {
        var encoder = new Base64Encoder();
        MemoryOwner<byte> buffer;

        await foreach (var chunk in bytes.WithCancellation(token).ConfigureAwait(false))
        {
            using (buffer = encoder.EncodeToUtf8(chunk.Span, allocator))
                yield return buffer.Memory;
        }

        if (encoder.HasBufferedData)
        {
            using (buffer = allocator.AllocateAtLeast(MaxBufferedDataSize))
            {
                var count = encoder.Flush(buffer.Span);
                yield return buffer.Memory.Slice(0, count);
            }
        }
    }

    /// <summary>
    /// Flushes the buffered data as base64-encoded UTF-8 characters to the output buffer.
    /// </summary>
    /// <param name="output">The output buffer of size 4.</param>
    /// <returns>The number of written bytes.</returns>
    public int Flush(scoped Span<byte> output)
    {
        int bytesWritten;

        if (reservedBufferSize is 0 || output.IsEmpty)
        {
            bytesWritten = 0;
        }
        else
        {
            Base64.EncodeToUtf8(BufferedData, output, out var consumed, out bytesWritten);
            reservedBufferSize -= consumed;
        }

        return bytesWritten;
    }
}
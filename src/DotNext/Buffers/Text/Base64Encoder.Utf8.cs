using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotNext.Buffers.Text;

using Buffers;
using Runtime.InteropServices;

public partial struct Base64Encoder
{
    private void EncodeToUtf8Core<TWriter>(scoped ReadOnlySpan<byte> bytes, scoped TWriter writer, bool flush)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var charsWritten = Base64.GetMaxEncodedToUtf8Length(bytes.Length);
        var buffer = writer.GetSpan(charsWritten);

        switch (Base64.EncodeToUtf8(bytes, buffer, out var bytesRead, out charsWritten, (bytes.Length % 3) is 0 || flush))
        {
            case OperationStatus.DestinationTooSmall or OperationStatus.Done:
                Reset();
                break;
            case OperationStatus.NeedMoreData:
                reservedBufferSize = bytes.Length - bytesRead;
                Debug.Assert(reservedBufferSize <= MaxBufferedDataSize);
                bytes.Slice(bytesRead).CopyTo(MemoryMarshal.AsBytes(ref reservedBuffer));
                break;
        }

        writer.Advance(charsWritten);
    }

    private void EncodeToUtf8Buffered<TWriter>(scoped ReadOnlySpan<byte> bytes, scoped TWriter writer, bool flush)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        if (HasBufferedData)
        {
            var tempBuffer = new SpanWriter<byte>(stackalloc byte[MaxBufferedDataSize + 1]);
            tempBuffer.Write(BufferedData);
            bytes = bytes.Slice(tempBuffer.Write(bytes));

            EncodeToUtf8Core(tempBuffer.WrittenSpan, writer, bytes.IsEmpty && flush);
        }

        if (bytes.Length > 0)
        {
            EncodeToUtf8Core(bytes, writer, flush);
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
    public MemoryOwner<byte> EncodeToUtf8(scoped ReadOnlySpan<byte> bytes, MemoryAllocator<byte>? allocator = null, bool flush = false)
    {
        if (bytes.Length > MaxInputSize)
            throw new ArgumentException(ExceptionMessages.LargeBuffer, nameof(bytes));

        var writer = new BufferWriterSlim<byte>(GetMaxEncodedLength(bytes.Length), allocator);
        EncodeToUtf8Buffered<BufferWriterSlim<byte>.Ref>(bytes, new(ref writer), flush);

        return writer.DetachOrCopyBuffer();
    }

    /// <inheritdoc/>
    MemoryOwner<byte> IBufferedEncoder<byte>.Encode(ReadOnlySpan<byte> bytes, MemoryAllocator<byte>? allocator) => EncodeToUtf8(bytes, allocator);

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
    public void EncodeToUtf8(scoped ReadOnlySpan<byte> bytes, scoped ref BufferWriterSlim<byte> chars, bool flush = false)
    {
        if (bytes.Length > MaxInputSize)
            throw new ArgumentException(ExceptionMessages.LargeBuffer, nameof(bytes));

        EncodeToUtf8Buffered<BufferWriterSlim<byte>.Ref>(bytes, new(ref chars), flush);
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
    public void EncodeToUtf8(scoped ReadOnlySpan<byte> bytes, IBufferWriter<byte> chars, bool flush = false)
    {
        ArgumentNullException.ThrowIfNull(chars);

        if (bytes.Length > MaxInputSize)
            throw new ArgumentException(ExceptionMessages.LargeBuffer, nameof(bytes));

        EncodeToUtf8Buffered<BufferWriterReference<byte>>(bytes, new(chars), flush);
    }

    /// <summary>
    /// Encodes a sequence of bytes to characters using base64 encoding.
    /// </summary>
    /// <param name="bytes">A collection of buffers.</param>
    /// <param name="allocator">Characters buffer allocator.</param>
    /// <param name="token">The token that can be used to cancel the encoding.</param>
    /// <returns>A collection of encoded bytes.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static IAsyncEnumerable<ReadOnlyMemory<byte>> EncodeToUtf8Async(IAsyncEnumerable<ReadOnlyMemory<byte>> bytes,
        MemoryAllocator<byte>? allocator = null, CancellationToken token = default)
        => IBufferedEncoder<byte>.EncodeAsync<Base64Encoder>(bytes, allocator.DefaultIfNull, token);

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
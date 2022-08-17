using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotNext.Buffers.Text;

using Buffers;
using StreamConsumer = IO.StreamConsumer;

public partial struct Base64Encoder
{
    private void EncodeToUtf8Core<TWriter>(ReadOnlySpan<byte> bytes, ref TWriter writer, bool flush)
        where TWriter : notnull, IBufferWriter<byte>
    {
        var produced = Base64.GetMaxEncodedToUtf8Length(bytes.Length);
        var buffer = writer.GetSpan(produced);

        switch (Base64.EncodeToUtf8(bytes, buffer, out var consumed, out produced, (bytes.Length % 3) is 0 || flush))
        {
            case OperationStatus.DestinationTooSmall or OperationStatus.Done:
                Reset();
                break;
            case OperationStatus.NeedMoreData:
                reservedBufferSize = bytes.Length - consumed;
                Debug.Assert(reservedBufferSize <= MaxBufferedDataSize);
                bytes.Slice(consumed).CopyTo(Span.AsBytes(ref reservedBuffer));
                break;
        }

        writer.Advance(produced);
    }

    [SkipLocalsInit]
    private void CopyAndEncodeToUtf8<TWriter>(ReadOnlySpan<byte> bytes, ref TWriter writer, bool flush)
        where TWriter : notnull, IBufferWriter<byte>
    {
        var newSize = reservedBufferSize + bytes.Length;
        using var tempBuffer = (uint)newSize <= (uint)MemoryRental<byte>.StackallocThreshold ? stackalloc byte[newSize] : new MemoryRental<byte>(newSize);
        Span.AsReadOnlyBytes(in reservedBuffer, reservedBufferSize).CopyTo(tempBuffer.Span);
        bytes.CopyTo(tempBuffer.Span.Slice(reservedBufferSize));
        EncodeToUtf8Core(tempBuffer.Span, ref writer, flush);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EncodeToUtf8<TWriter>(ReadOnlySpan<byte> bytes, ref TWriter writer, bool flush)
        where TWriter : notnull, IBufferWriter<byte>
    {
        Debug.Assert(bytes.Length <= MaxInputSize);

        if (HasBufferedData)
            CopyAndEncodeToUtf8(bytes, ref writer, flush);
        else
            EncodeToUtf8Core(bytes, ref writer, flush);
    }

    /// <summary>
    /// Encodes a block of bytes to base64-encoded UTF-8 characters.
    /// </summary>
    /// <param name="bytes">A block of bytes to encode.</param>
    /// <param name="output">The output buffer.</param>
    /// <param name="flush">
    /// <see langword="true"/> to encode the final block and insert padding if necessary;
    /// <see langword="false"/> to encode a fragment without padding.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The length of <paramref name="bytes"/> is greater than <see cref="MaxInputSize"/>.</exception>
    public void EncodeToUtf8(ReadOnlySpan<byte> bytes, IBufferWriter<byte> output, bool flush = false)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (bytes.Length > MaxInputSize)
            throw new ArgumentException(ExceptionMessages.LargeBuffer, nameof(bytes));

        EncodeToUtf8(bytes, ref output, flush);
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

        var result = new MemoryOwnerWrapper<byte>(allocator);
        EncodeToUtf8(bytes, ref result, flush);
        return result.Buffer;
    }

    [SkipLocalsInit]
    private void EncodeToUtf8Core<TConsumer>(ReadOnlySpan<byte> bytes, TConsumer output, bool flush)
        where TConsumer : notnull, IReadOnlySpanConsumer<byte>
    {
        Span<byte> buffer = stackalloc byte[EncodingBufferSize];

    consume_next_chunk:
        switch (Base64.EncodeToUtf8(bytes, buffer, out var consumed, out var produced, (bytes.Length % 3) is 0))
        {
            case OperationStatus.DestinationTooSmall or OperationStatus.Done:
                Reset();
                break;
            case OperationStatus.NeedMoreData:
                reservedBufferSize = bytes.Length - consumed;
                Debug.Assert(reservedBufferSize <= MaxBufferedDataSize);
                bytes.Slice(consumed).CopyTo(Span.AsBytes(ref reservedBuffer));
                break;
        }

        if (produced > 0 && consumed > 0)
        {
            output.Invoke(buffer.Slice(0, produced));
            bytes = bytes.Slice(consumed);
            goto consume_next_chunk;
        }

        // flush the rest of the buffer
        if (HasBufferedData && flush)
        {
            Base64.EncodeToUtf8(Span.AsReadOnlyBytes(in reservedBuffer, reservedBufferSize), buffer, out consumed, out produced);
            Reset();
            output.Invoke(buffer.Slice(0, produced));
        }
    }

    [SkipLocalsInit]
    private void CopyAndEncodeToUtf8<TConsumer>(ReadOnlySpan<byte> bytes, TConsumer output, bool flush)
        where TConsumer : notnull, IReadOnlySpanConsumer<byte>
    {
        var newSize = reservedBufferSize + bytes.Length;
        using var tempBuffer = (uint)newSize <= (uint)MemoryRental<byte>.StackallocThreshold ? stackalloc byte[newSize] : new MemoryRental<byte>(newSize);
        Span.AsReadOnlyBytes(in reservedBuffer, reservedBufferSize).CopyTo(tempBuffer.Span);
        bytes.CopyTo(tempBuffer.Span.Slice(reservedBufferSize));
        EncodeToUtf8Core(tempBuffer.Span, output, flush);
    }

    /// <summary>
    /// Encodes a block of bytes to base64-encoded UTF-8 characters.
    /// </summary>
    /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
    /// <param name="bytes">A block of bytes to encode.</param>
    /// <param name="output">The consumer called for encoded portion of data.</param>
    /// <param name="flush">
    /// <see langword="true"/> to encode the final block and insert padding if necessary;
    /// <see langword="false"/> to encode a fragment without padding.
    /// </param>
    public void EncodeToUtf8<TConsumer>(ReadOnlySpan<byte> bytes, TConsumer output, bool flush = false)
        where TConsumer : notnull, IReadOnlySpanConsumer<byte>
    {
        if (HasBufferedData)
            CopyAndEncodeToUtf8(bytes, output, flush);
        else
            EncodeToUtf8Core(bytes, output, flush);
    }

    /// <summary>
    /// Encodes a block of bytes to base64-encoded UTF-8 characters.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument to be passed to the callback.</typeparam>
    /// <param name="bytes">A block of bytes to encode.</param>
    /// <param name="output">The consumer called for encoded portion of data.</param>
    /// <param name="arg">The argument to be passed to the callback.</param>
    /// <param name="flush">
    /// <see langword="true"/> to encode the final block and insert padding if necessary;
    /// <see langword="false"/> to encode a fragment without padding.
    /// </param>
    public void EncodeToUtf8<TArg>(ReadOnlySpan<byte> bytes, ReadOnlySpanAction<byte, TArg> output, TArg arg, bool flush = false)
        => EncodeToUtf8(bytes, new DelegatingReadOnlySpanConsumer<byte, TArg>(output, arg), flush);

    /// <summary>
    /// Encodes a block of bytes to base64-encoded UTF-8 characters.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument to be passed to the callback.</typeparam>
    /// <param name="bytes">A block of bytes to encode.</param>
    /// <param name="output">The consumer called for encoded portion of data.</param>
    /// <param name="arg">The argument to be passed to the callback.</param>
    /// <param name="flush">
    /// <see langword="true"/> to encode the final block and insert padding if necessary;
    /// <see langword="false"/> to encode a fragment without padding.
    /// </param>
    [CLSCompliant(false)]
    public unsafe void EncodeToUtf8<TArg>(ReadOnlySpan<byte> bytes, delegate*<ReadOnlySpan<byte>, TArg, void> output, TArg arg, bool flush = false)
        => EncodeToUtf8(bytes, new ReadOnlySpanConsumer<byte, TArg>(output, arg), flush);

    /// <summary>
    /// Encodes a block of bytes to base64-encoded UTF-8 characters.
    /// </summary>
    /// <param name="bytes">A block of bytes to encode.</param>
    /// <param name="output">The stream used as a destination for encoded data.</param>
    /// <param name="flush">
    /// <see langword="true"/> to encode the final block and insert padding if necessary;
    /// <see langword="false"/> to encode a fragment without padding.
    /// </param>
    public void EncodeToUtf8(ReadOnlySpan<byte> bytes, Stream output, bool flush = false)
        => EncodeToUtf8<StreamConsumer>(bytes, output, flush);

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
            using (buffer = allocator.Invoke(MaxBufferedDataSize, exactSize: false))
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
    public int Flush(Span<byte> output)
    {
        int bytesWritten;

        if (reservedBufferSize is 0 || output.IsEmpty)
        {
            bytesWritten = 0;
        }
        else
        {
            Base64.EncodeToUtf8(Span.AsReadOnlyBytes(in reservedBuffer, reservedBufferSize), output, out var consumed, out bytesWritten);
            reservedBufferSize -= consumed;
        }

        return bytesWritten;
    }
}
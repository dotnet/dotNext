using System.Buffers;

namespace DotNext.Text;

using Buffers;
using NewBase64Encoder = Buffers.Text.Base64Encoder;

public partial struct Base64Encoder
{
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
        => encoder.EncodeToUtf8(bytes, output, flush);

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
        => encoder.EncodeToUtf8(bytes, allocator, flush);

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
        => encoder.EncodeToUtf8(bytes, output, flush);

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
        => encoder.EncodeToUtf8(bytes, output, arg, flush);

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
        => encoder.EncodeToUtf8(bytes, output, arg, flush);

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
        => encoder.EncodeToUtf8(bytes, output, flush);

    /// <summary>
    /// Encodes a sequence of bytes to characters using base64 encoding.
    /// </summary>
    /// <param name="bytes">A collection of buffers.</param>
    /// <param name="allocator">Characters buffer allocator.</param>
    /// <param name="token">The token that can be used to cancel the encoding.</param>
    /// <returns>A collection of encoded bytes.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static IAsyncEnumerable<ReadOnlyMemory<byte>> EncodeToUtf8Async(IAsyncEnumerable<ReadOnlyMemory<byte>> bytes, MemoryAllocator<byte>? allocator = null, CancellationToken token = default)
        => NewBase64Encoder.EncodeToUtf8Async(bytes, allocator, token);

    /// <summary>
    /// Flushes the buffered data as base64-encoded UTF-8 characters to the output buffer.
    /// </summary>
    /// <param name="output">The output buffer of size 4.</param>
    /// <returns>The number of written bytes.</returns>
    public int Flush(Span<byte> output)
        => encoder.Flush(output);
}
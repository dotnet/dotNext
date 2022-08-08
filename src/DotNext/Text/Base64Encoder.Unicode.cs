using System.Buffers;
using System.Text;

namespace DotNext.Text;

using Buffers;
using NewBase64Encoder = Buffers.Text.Base64Encoder;

public partial struct Base64Encoder
{
    /// <summary>
    /// Encodes a block of bytes to base64-encoded characters.
    /// </summary>
    /// <param name="bytes">A block of bytes to encode.</param>
    /// <param name="output">The output buffer.</param>
    /// <param name="flush">
    /// <see langword="true"/> to encode the final block and insert padding if necessary;
    /// <see langword="false"/> to encode a fragment without padding.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The length of <paramref name="bytes"/> is greater than <see cref="MaxInputSize"/>.</exception>
    public void EncodeToChars(ReadOnlySpan<byte> bytes, IBufferWriter<char> output, bool flush = false)
        => encoder.EncodeToChars(bytes, output, flush);

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
    public MemoryOwner<char> EncodeToChars(ReadOnlySpan<byte> bytes, MemoryAllocator<char>? allocator = null, bool flush = false)
        => encoder.EncodeToChars(bytes, allocator, flush);

    /// <summary>
    /// Encodes a block of bytes to base64-encoded characters.
    /// </summary>
    /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
    /// <param name="bytes">A block of bytes to encode.</param>
    /// <param name="output">The consumer called for encoded portion of data.</param>
    /// <param name="flush">
    /// <see langword="true"/> to encode the final block and insert padding if necessary;
    /// <see langword="false"/> to encode a fragment without padding.
    /// </param>
    public void EncodeToChars<TConsumer>(ReadOnlySpan<byte> bytes, TConsumer output, bool flush = false)
        where TConsumer : notnull, IReadOnlySpanConsumer<char>
        => encoder.EncodeToChars(bytes, output, flush);

    /// <summary>
    /// Encodes a block of bytes to base64-encoded characters.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument to be passed to the callback.</typeparam>
    /// <param name="bytes">A block of bytes to encode.</param>
    /// <param name="output">The consumer called for encoded portion of data.</param>
    /// <param name="arg">The argument to be passed to the callback.</param>
    /// <param name="flush">
    /// <see langword="true"/> to encode the final block and insert padding if necessary;
    /// <see langword="false"/> to encode a fragment without padding.
    /// </param>
    public void EncodeToChars<TArg>(ReadOnlySpan<byte> bytes, ReadOnlySpanAction<char, TArg> output, TArg arg, bool flush = false)
        => encoder.EncodeToChars(bytes, output, arg, flush);

    /// <summary>
    /// Encodes a block of bytes to base64-encoded characters.
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
    public unsafe void EncodeToChars<TArg>(ReadOnlySpan<byte> bytes, delegate*<ReadOnlySpan<char>, TArg, void> output, TArg arg, bool flush = false)
        => encoder.EncodeToChars(bytes, output, arg, flush);

    /// <summary>
    /// Encodes a block of bytes to base64-encoded characters.
    /// </summary>
    /// <param name="bytes">A block of bytes to encode.</param>
    /// <param name="output">The writer used as a destination for encoded data.</param>
    /// <param name="flush">
    /// <see langword="true"/> to encode the final block and insert padding if necessary;
    /// <see langword="false"/> to encode a fragment without padding.
    /// </param>
    public void EncodeToChars(ReadOnlySpan<byte> bytes, TextWriter output, bool flush = false)
        => encoder.EncodeToChars(bytes, output, flush);

    /// <summary>
    /// Encodes a block of bytes to base64-encoded characters.
    /// </summary>
    /// <param name="bytes">A block of bytes to encode.</param>
    /// <param name="output">The builder used as a destination for encoded data.</param>
    /// <param name="flush">
    /// <see langword="true"/> to encode the final block and insert padding if necessary;
    /// <see langword="false"/> to encode a fragment without padding.
    /// </param>
    public void EncodeToChars(ReadOnlySpan<byte> bytes, StringBuilder output, bool flush = false)
        => encoder.EncodeToChars(bytes, output, flush);

    /// <summary>
    /// Encodes a sequence of bytes to characters using base64 encoding.
    /// </summary>
    /// <param name="bytes">A collection of buffers.</param>
    /// <param name="allocator">Characters buffer allocator.</param>
    /// <param name="token">The token that can be used to cancel the encoding.</param>
    /// <returns>A collection of encoded bytes.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static IAsyncEnumerable<ReadOnlyMemory<char>> EncodeToCharsAsync(IAsyncEnumerable<ReadOnlyMemory<byte>> bytes, MemoryAllocator<char>? allocator = null, CancellationToken token = default)
        => NewBase64Encoder.EncodeToCharsAsync(bytes, allocator, token);

    /// <summary>
    /// Flushes the buffered data as base64-encoded characters to the output buffer.
    /// </summary>
    /// <param name="output">The buffer of characters.</param>
    /// <returns>The number of written characters.</returns>
    public int Flush(Span<char> output)
        => encoder.Flush(output);
}
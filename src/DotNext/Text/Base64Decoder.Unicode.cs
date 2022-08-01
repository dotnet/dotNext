using System.Buffers;

namespace DotNext.Text;

using Buffers;
using NewBase64Decoder = Buffers.Text.Base64Decoder;

public partial struct Base64Decoder
{
    /// <summary>
    /// Decodes base64 characters.
    /// </summary>
    /// <param name="chars">The span containing base64-encoded bytes.</param>
    /// <param name="output">The output growable buffer used to write decoded bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    public void Decode(ReadOnlySpan<char> chars, IBufferWriter<byte> output)
        => decoder.Decode(chars, output);

    /// <summary>
    /// Decodes base64 characters.
    /// </summary>
    /// <param name="chars">The span containing base64-encoded bytes.</param>
    /// <param name="output">The output growable buffer used to write decoded bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    public void Decode(in ReadOnlySequence<char> chars, IBufferWriter<byte> output)
        => decoder.Decode(in chars, output);

    /// <summary>
    /// Decodes base64 characters.
    /// </summary>
    /// <param name="chars">The span containing base64-encoded bytes.</param>
    /// <param name="allocator">The allocator of the result buffer.</param>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    /// <returns>A buffer containing decoded bytes.</returns>
    public MemoryOwner<byte> Decode(ReadOnlySpan<char> chars, MemoryAllocator<byte>? allocator = null)
        => decoder.Decode(chars, allocator);

    /// <summary>
    /// Decodes base64-encoded bytes.
    /// </summary>
    /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
    /// <param name="chars">The span containing base64-encoded bytes.</param>
    /// <param name="output">The consumer called for decoded portion of data.</param>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    public void Decode<TConsumer>(ReadOnlySpan<char> chars, TConsumer output)
        where TConsumer : notnull, IReadOnlySpanConsumer<byte>
        => decoder.Decode(chars, output);

    /// <summary>
    /// Decodes base64-encoded bytes.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument to be passed to the callback.</typeparam>
    /// <param name="chars">The span containing base64-encoded bytes.</param>
    /// <param name="callback">The callback called for decoded portion of data.</param>
    /// <param name="arg">The argument to be passed to the callback.</param>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    public void Decode<TArg>(ReadOnlySpan<char> chars, ReadOnlySpanAction<byte, TArg> callback, TArg arg)
        => decoder.Decode(chars, callback, arg);

    /// <summary>
    /// Decodes base64-encoded bytes.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument to be passed to the callback.</typeparam>
    /// <param name="chars">The span containing base64-encoded bytes.</param>
    /// <param name="callback">The callback called for decoded portion of data.</param>
    /// <param name="arg">The argument to be passed to the callback.</param>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    [CLSCompliant(false)]
    public unsafe void Decode<TArg>(ReadOnlySpan<char> chars, delegate*<ReadOnlySpan<byte>, TArg, void> callback, TArg arg)
        => decoder.Decode(chars, callback, arg);

    /// <summary>
    /// Decodes a sequence of base64-encoded bytes.
    /// </summary>
    /// <param name="chars">A sequence of base64-encoded bytes.</param>
    /// <param name="allocator">The allocator of the buffer used for decoded bytes.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>A sequence of decoded bytes.</returns>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static IAsyncEnumerable<ReadOnlyMemory<byte>> DecodeAsync(IAsyncEnumerable<ReadOnlyMemory<char>> chars, MemoryAllocator<byte>? allocator = null, CancellationToken token = default)
        => NewBase64Decoder.DecodeAsync(chars, allocator, token);
}
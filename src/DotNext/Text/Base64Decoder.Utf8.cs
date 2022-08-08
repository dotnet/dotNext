using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotNext.Text;

using Buffers;
using NewBase64Decoder = Buffers.Text.Base64Decoder;

public partial struct Base64Decoder
{
    /// <summary>
    /// Decodes UTF-8 encoded base64 string.
    /// </summary>
    /// <param name="utf8Chars">UTF-8 encoded portion of base64 string.</param>
    /// <param name="output">The output growable buffer used to write decoded bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    public void Decode(ReadOnlySpan<byte> utf8Chars, IBufferWriter<byte> output)
        => decoder.DecodeFromUtf8(utf8Chars, output);

    /// <summary>
    /// Decodes UTF-8 encoded base64 string.
    /// </summary>
    /// <param name="utf8Chars">UTF-8 encoded portion of base64 string.</param>
    /// <param name="output">The output growable buffer used to write decoded bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    public void Decode(in ReadOnlySequence<byte> utf8Chars, IBufferWriter<byte> output)
        => decoder.DecodeFromUtf8(in utf8Chars, output);

    /// <summary>
    /// Decoes UTF-8 encoded base64 string.
    /// </summary>
    /// <param name="utf8Chars">UTF-8 encoded portion of base64 string.</param>
    /// <param name="allocator">The allocator of the result buffer.</param>
    /// <returns>A buffer containing decoded bytes.</returns>
    public MemoryOwner<byte> Decode(ReadOnlySpan<byte> utf8Chars, MemoryAllocator<byte>? allocator = null)
        => decoder.DecodeFromUtf8(utf8Chars, allocator);

    /// <summary>
    /// Decodes base64-encoded bytes.
    /// </summary>
    /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
    /// <param name="utf8Chars">The span containing base64-encoded bytes.</param>
    /// <param name="output">The consumer called for decoded portion of data.</param>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    public void Decode<TConsumer>(ReadOnlySpan<byte> utf8Chars, TConsumer output)
        where TConsumer : notnull, IReadOnlySpanConsumer<byte>
        => decoder.DecodeFromUtf8(utf8Chars, output);

    /// <summary>
    /// Decodes UTF-8 encoded base64 string.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument to be passed to the callback.</typeparam>
    /// <param name="utf8Chars">UTF-8 encoded portion of base64 string.</param>
    /// <param name="output">The callback called for decoded portion of data.</param>
    /// <param name="arg">The argument to be passed to the callback.</param>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    public void Decode<TArg>(ReadOnlySpan<byte> utf8Chars, ReadOnlySpanAction<byte, TArg> output, TArg arg)
        => decoder.DecodeFromUtf8(utf8Chars, output, arg);

    /// <summary>
    /// Decodes UTF-8 encoded base64 string.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument to be passed to the callback.</typeparam>
    /// <param name="utf8Chars">UTF-8 encoded portion of base64 string.</param>
    /// <param name="output">The callback called for decoded portion of data.</param>
    /// <param name="arg">The argument to be passed to the callback.</param>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    [CLSCompliant(false)]
    public unsafe void Decode<TArg>(ReadOnlySpan<byte> utf8Chars, delegate*<ReadOnlySpan<byte>, TArg, void> output, TArg arg)
        => decoder.DecodeFromUtf8(utf8Chars, output, arg);

    /// <summary>
    /// Decodes UTF-8 encoded base64 string and writes result to the stream synchronously.
    /// </summary>
    /// <param name="utf8Chars">UTF-8 encoded portion of base64 string.</param>
    /// <param name="output">The stream used as destination for decoded bytes.</param>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    public void Decode(ReadOnlySpan<byte> utf8Chars, Stream output)
        => decoder.DecodeFromUtf8(utf8Chars, output);

    /// <summary>
    /// Decodes a sequence of base64-encoded bytes.
    /// </summary>
    /// <param name="utf8Chars">A sequence of base64-encoded bytes.</param>
    /// <param name="allocator">The allocator of the buffer used for decoded bytes.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>A sequence of decoded bytes.</returns>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static IAsyncEnumerable<ReadOnlyMemory<byte>> DecodeAsync(IAsyncEnumerable<ReadOnlyMemory<byte>> utf8Chars, MemoryAllocator<byte>? allocator = null, CancellationToken token = default)
        => NewBase64Decoder.DecodeFromUtf8Async(utf8Chars, allocator, token);
}
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers.Text;

using Buffers;

public partial struct Base64Decoder
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Span<char> AsChars(ref ulong value)
        => MemoryMarshal.CreateSpan(ref Unsafe.As<ulong, char>(ref value), sizeof(ulong) / sizeof(char));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReadOnlySpan<char> AsChars(in ulong value, int length)
    {
        Debug.Assert((uint)length <= (uint)(sizeof(ulong) / sizeof(char)));

        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<ulong, char>(ref Unsafe.AsRef(in value)), length);
    }

    private bool DecodeFromUtf16Core<TWriter>(scoped ReadOnlySpan<char> chars, scoped ref TWriter writer)
        where TWriter : notnull, IBufferWriter<byte>
    {
        var size = chars.Length & 3;
        if (size > 0)
        {
            // size of the rest
            size = chars.Length - size;
            var rest = chars.Slice(size);
            rest.CopyTo(AsChars(ref reservedBuffer));
            reservedBufferSize = rest.Length; // keep the number of chars, not bytes
            chars = chars.Slice(0, size);
        }
        else
        {
            Reset();
        }

        // 4 characters => 3 bytes
        if (!Convert.TryFromBase64Chars(chars, writer.GetSpan(chars.Length), out size))
            return false;

        writer.Advance(size);
        return true;
    }

    [SkipLocalsInit]
    private bool CopyAndDecodeFromUtf16<TWriter>(scoped ReadOnlySpan<char> chars, scoped ref TWriter writer)
        where TWriter : notnull, IBufferWriter<byte>
    {
        var newSize = reservedBufferSize + chars.Length;
        using var tempBuffer = (uint)newSize <= (uint)MemoryRental<char>.StackallocThreshold ? stackalloc char[newSize] : new MemoryRental<char>(newSize);
        AsChars(in reservedBuffer, reservedBufferSize).CopyTo(tempBuffer.Span);
        chars.CopyTo(tempBuffer.Span.Slice(reservedBufferSize));
        return DecodeFromUtf16Core(tempBuffer.Span, ref writer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool DecodeFromUtf16<TWriter>(scoped ReadOnlySpan<char> chars, scoped ref TWriter writer)
        where TWriter : notnull, IBufferWriter<byte>
        => NeedMoreData ? CopyAndDecodeFromUtf16(chars, ref writer) : DecodeFromUtf16Core(chars, ref writer);

    /// <summary>
    /// Decodes base64 characters.
    /// </summary>
    /// <param name="chars">The span containing base64-encoded bytes.</param>
    /// <param name="output">The output growable buffer used to write decoded bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    public void DecodeFromUtf16(scoped ReadOnlySpan<char> chars, IBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (!DecodeFromUtf16(chars, ref output))
            throw new FormatException(ExceptionMessages.MalformedBase64);
    }

    /// <summary>
    /// Decodes base64 characters.
    /// </summary>
    /// <param name="chars">The span containing base64-encoded bytes.</param>
    /// <param name="output">The output growable buffer used to write decoded bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    public void DecodeFromUtf16(scoped in ReadOnlySequence<char> chars, IBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);

        foreach (var chunk in chars)
        {
            if (!DecodeFromUtf16(chunk.Span, ref output))
                throw new FormatException(ExceptionMessages.MalformedBase64);
        }
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
        var result = new MemoryOwnerWrapper<byte>(allocator);

        if (chars.IsEmpty || DecodeFromUtf16(chars, ref result))
            return result.Buffer;

        result.Buffer.Dispose();
        throw new FormatException(ExceptionMessages.MalformedBase64);
    }

    [SkipLocalsInit]
    private void DecodeFromUtf16Core<TConsumer>(scoped ReadOnlySpan<char> chars, TConsumer output)
        where TConsumer : notnull, IReadOnlySpanConsumer<byte>
    {
        const int maxInputBlockSize = (DecodingBufferSize / 3) * 4;
        Span<byte> buffer = stackalloc byte[DecodingBufferSize];

    consume_next_chunk:
        var chunk = chars.TrimLength(maxInputBlockSize);
        if (Decode(chunk, buffer, out var consumed, out var produced))
        {
            Reset();
        }
        else
        {
            reservedBufferSize = chunk.Length - consumed;
            Debug.Assert(reservedBufferSize <= 4);
            chunk.Slice(consumed).CopyTo(AsChars(ref reservedBuffer));
        }

        if (consumed > 0 && produced > 0)
        {
            output.Invoke(buffer.Slice(0, produced));
            chars = chars.Slice(consumed);
            goto consume_next_chunk;
        }

        // true - encoding completed, false - need more data
        static bool Decode(scoped ReadOnlySpan<char> input, scoped Span<byte> output, out int consumedChars, out int producedBytes)
        {
            Debug.Assert(output.Length == DecodingBufferSize);
            Debug.Assert(input.Length <= maxInputBlockSize);
            bool result;
            int rest;

            // x & 3 is the same as x % 4
            if (result = (rest = input.Length & 3) is 0)
                consumedChars = input.Length;
            else
                input = input.Slice(0, consumedChars = input.Length - rest);

            return Convert.TryFromBase64Chars(input, output, out producedBytes) ?
                result :
                throw new FormatException(ExceptionMessages.MalformedBase64);
        }
    }

    [SkipLocalsInit]
    private void CopyAndDecodeFromUtf16<TConsumer>(scoped ReadOnlySpan<char> chars, TConsumer output)
        where TConsumer : notnull, IReadOnlySpanConsumer<byte>
    {
        var newSize = reservedBufferSize + chars.Length;
        using var tempBuffer = (uint)newSize <= (uint)MemoryRental<char>.StackallocThreshold ? stackalloc char[newSize] : new MemoryRental<char>(newSize);
        AsChars(in reservedBuffer, reservedBufferSize).CopyTo(tempBuffer.Span);
        chars.CopyTo(tempBuffer.Span.Slice(reservedBufferSize));
        DecodeFromUtf16Core(tempBuffer.Span, output);
    }

    /// <summary>
    /// Decodes base64-encoded bytes.
    /// </summary>
    /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
    /// <param name="chars">The span containing base64-encoded bytes.</param>
    /// <param name="output">The consumer called for decoded portion of data.</param>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    public void DecodeFromUtf16<TConsumer>(scoped ReadOnlySpan<char> chars, TConsumer output)
        where TConsumer : notnull, IReadOnlySpanConsumer<byte>
    {
        if (NeedMoreData)
            CopyAndDecodeFromUtf16(chars, output);
        else
            DecodeFromUtf16Core(chars, output);
    }

    /// <summary>
    /// Decodes base64-encoded bytes.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument to be passed to the callback.</typeparam>
    /// <param name="chars">The span containing base64-encoded bytes.</param>
    /// <param name="callback">The callback called for decoded portion of data.</param>
    /// <param name="arg">The argument to be passed to the callback.</param>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    public void DecodeFromUtf16<TArg>(scoped ReadOnlySpan<char> chars, ReadOnlySpanAction<byte, TArg> callback, TArg arg)
        => DecodeFromUtf16(chars, new DelegatingReadOnlySpanConsumer<byte, TArg>(callback, arg));

    /// <summary>
    /// Decodes base64-encoded bytes.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument to be passed to the callback.</typeparam>
    /// <param name="chars">The span containing base64-encoded bytes.</param>
    /// <param name="callback">The callback called for decoded portion of data.</param>
    /// <param name="arg">The argument to be passed to the callback.</param>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    [CLSCompliant(false)]
    public unsafe void DecodeFromUtf16<TArg>(scoped ReadOnlySpan<char> chars, delegate*<ReadOnlySpan<byte>, TArg, void> callback, TArg arg)
        => DecodeFromUtf16(chars, new ReadOnlySpanConsumer<byte, TArg>(callback, arg));

    /// <summary>
    /// Decodes a sequence of base64-encoded bytes.
    /// </summary>
    /// <param name="chars">A sequence of base64-encoded bytes.</param>
    /// <param name="allocator">The allocator of the buffer used for decoded bytes.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>A sequence of decoded bytes.</returns>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async IAsyncEnumerable<ReadOnlyMemory<byte>> DecodeFromUtf16Async(IAsyncEnumerable<ReadOnlyMemory<char>> chars, MemoryAllocator<byte>? allocator = null, [EnumeratorCancellation] CancellationToken token = default)
    {
        var decoder = new Base64Decoder();
        MemoryOwner<byte> buffer;

        await foreach (var chunk in chars.WithCancellation(token).ConfigureAwait(false))
        {
            using (buffer = decoder.DecodeFromUtf16(chunk.Span, allocator))
                yield return buffer.Memory;
        }

        if (decoder.NeedMoreData)
            throw new FormatException(ExceptionMessages.MalformedBase64);
    }
}
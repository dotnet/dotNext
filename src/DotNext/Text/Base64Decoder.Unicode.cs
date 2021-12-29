using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Text;

using Buffers;

public partial struct Base64Decoder
{
    private Span<char> ReservedChars => MemoryMarshal.CreateSpan(ref Unsafe.As<ulong, char>(ref reservedBuffer), sizeof(ulong) / sizeof(char));

    private bool DecodeCore<TWriter>(ReadOnlySpan<char> chars, ref TWriter writer)
        where TWriter : notnull, IBufferWriter<byte>
    {
        var size = chars.Length & 3;
        if (size > 0)
        {
            // size of the rest
            size = chars.Length - size;
            var rest = chars.Slice(size);
            rest.CopyTo(ReservedChars);
            reservedBufferSize = rest.Length; // keep the number of chars, not bytes
            chars = chars.Slice(0, size);
        }
        else
        {
            reservedBufferSize = 0;
        }

        // 4 characters => 3 bytes
        if (!Convert.TryFromBase64Chars(chars, writer.GetSpan(chars.Length), out size))
            return false;

        writer.Advance(size);
        return true;
    }

    [SkipLocalsInit]
    private bool CopyAndDecode<TWriter>(ReadOnlySpan<char> chars, ref TWriter writer)
        where TWriter : notnull, IBufferWriter<byte>
    {
        var newSize = reservedBufferSize + chars.Length;
        using var tempBuffer = (uint)newSize <= (uint)MemoryRental<char>.StackallocThreshold ? stackalloc char[newSize] : new MemoryRental<char>(newSize);
        ReservedChars.Slice(0, reservedBufferSize).CopyTo(tempBuffer.Span);
        chars.CopyTo(tempBuffer.Span.Slice(reservedBufferSize));
        return DecodeCore(tempBuffer.Span, ref writer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool Decode<TWriter>(ReadOnlySpan<char> chars, ref TWriter writer)
        where TWriter : notnull, IBufferWriter<byte>
        => NeedMoreData ? CopyAndDecode(chars, ref writer) : DecodeCore(chars, ref writer);

    /// <summary>
    /// Decodes base64 characters.
    /// </summary>
    /// <param name="chars">The span containing base64-encoded bytes.</param>
    /// <param name="output">The output growable buffer used to write decoded bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    public void Decode(ReadOnlySpan<char> chars, IBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (!Decode(chars, ref output))
            throw new FormatException(ExceptionMessages.MalformedBase64);
    }

    /// <summary>
    /// Decodes base64 characters.
    /// </summary>
    /// <param name="chars">The span containing base64-encoded bytes.</param>
    /// <param name="output">The output growable buffer used to write decoded bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    public void Decode(in ReadOnlySequence<char> chars, IBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);

        foreach (var chunk in chars)
        {
            if (!Decode(chunk.Span, ref output))
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
    public MemoryOwner<byte> Decode(ReadOnlySpan<char> chars, MemoryAllocator<byte>? allocator = null)
    {
        var result = new MemoryOwnerWrapper<byte>(allocator);

        if (chars.IsEmpty || Decode(chars, ref result))
            return result.Buffer;

        result.Buffer.Dispose();
        throw new FormatException(ExceptionMessages.MalformedBase64);
    }

    [SkipLocalsInit]
    private void DecodeCore<TConsumer>(ReadOnlySpan<char> chars, TConsumer output)
        where TConsumer : notnull, IReadOnlySpanConsumer<byte>
    {
        const int maxInputBlockSize = (DecodingBufferSize / 3) * 4;
        Span<byte> buffer = stackalloc byte[DecodingBufferSize];

    consume_next_chunk:
        var chunk = chars.TrimLength(maxInputBlockSize);
        if (Decode(chunk, buffer, out var consumed, out var produced))
        {
            reservedBufferSize = 0;
        }
        else
        {
            reservedBufferSize = chunk.Length - consumed;
            Debug.Assert(reservedBufferSize <= 4);
            chunk.Slice(consumed).CopyTo(ReservedChars);
        }

        if (consumed > 0 && produced > 0)
        {
            output.Invoke(buffer.Slice(0, produced));
            chars = chars.Slice(consumed);
            goto consume_next_chunk;
        }

        // true - encoding completed, false - need more data
        static bool Decode(ReadOnlySpan<char> input, Span<byte> output, out int consumedChars, out int producedBytes)
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
    private void CopyAndDecode<TConsumer>(ReadOnlySpan<char> chars, TConsumer output)
        where TConsumer : notnull, IReadOnlySpanConsumer<byte>
    {
        var newSize = reservedBufferSize + chars.Length;
        using var tempBuffer = (uint)newSize <= (uint)MemoryRental<char>.StackallocThreshold ? stackalloc char[newSize] : new MemoryRental<char>(newSize);
        ReservedChars.Slice(0, reservedBufferSize).CopyTo(tempBuffer.Span);
        chars.CopyTo(tempBuffer.Span.Slice(reservedBufferSize));
        DecodeCore(tempBuffer.Span, output);
    }

    /// <summary>
    /// Decodes base64-encoded bytes.
    /// </summary>
    /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
    /// <param name="chars">The span containing base64-encoded bytes.</param>
    /// <param name="output">The consumer called for decoded portion of data.</param>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    public void Decode<TConsumer>(ReadOnlySpan<char> chars, TConsumer output)
        where TConsumer : notnull, IReadOnlySpanConsumer<byte>
    {
        if (NeedMoreData)
            CopyAndDecode(chars, output);
        else
            DecodeCore(chars, output);
    }

    /// <summary>
    /// Decodes base64-encoded bytes.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument to be passed to the callback.</typeparam>
    /// <param name="chars">The span containing base64-encoded bytes.</param>
    /// <param name="callback">The callback called for decoded portion of data.</param>
    /// <param name="arg">The argument to be passed to the callback.</param>
    /// <exception cref="FormatException">The input base64 string is malformed.</exception>
    public void Decode<TArg>(ReadOnlySpan<char> chars, ReadOnlySpanAction<byte, TArg> callback, TArg arg)
        => Decode(chars, new DelegatingReadOnlySpanConsumer<byte, TArg>(callback, arg));

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
        => Decode(chars, new ReadOnlySpanConsumer<byte, TArg>(callback, arg));
}
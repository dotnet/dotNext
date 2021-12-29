using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Unicode;

namespace DotNext.Text;

using Buffers;
using TextConsumer = IO.TextConsumer;

public partial struct Base64Encoder
{
    private void EncodeToCharsCore<TWriter>(ReadOnlySpan<byte> bytes, ref TWriter writer, bool flush)
        where TWriter : notnull, IBufferWriter<char>
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
            rest.CopyTo(ReservedBytes);
            reservedBufferSize = rest.Length;
            bytes = bytes.Slice(0, size);
        }

        Convert.TryToBase64Chars(bytes, writer.GetSpan(Base64.GetMaxEncodedToUtf8Length(bytes.Length)), out size);
        writer.Advance(size);
    }

    [SkipLocalsInit]
    private void CopyAndEncodeToChars<TWriter>(ReadOnlySpan<byte> bytes, ref TWriter writer, bool flush)
        where TWriter : notnull, IBufferWriter<char>
    {
        var newSize = reservedBufferSize + bytes.Length;
        using var tempBuffer = (uint)newSize <= (uint)MemoryRental<byte>.StackallocThreshold ? stackalloc byte[newSize] : new MemoryRental<byte>(newSize);
        ReservedBytes.Slice(0, reservedBufferSize).CopyTo(tempBuffer.Span);
        bytes.CopyTo(tempBuffer.Span.Slice(reservedBufferSize));
        EncodeToCharsCore(tempBuffer.Span, ref writer, flush);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EncodeToChars<TWriter>(ReadOnlySpan<byte> bytes, ref TWriter writer, bool flush)
        where TWriter : notnull, IBufferWriter<char>
    {
        Debug.Assert(bytes.Length <= MaxInputSize);

        if (HasBufferedData)
            CopyAndEncodeToChars(bytes, ref writer, flush);
        else
            EncodeToCharsCore(bytes, ref writer, flush);
    }

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
    {
        ArgumentNullException.ThrowIfNull(output);

        if (bytes.Length >= MaxInputSize)
            throw new ArgumentException(ExceptionMessages.LargeBuffer, nameof(bytes));

        EncodeToChars(bytes, ref output, flush);
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
    public MemoryOwner<char> EncodeToChars(ReadOnlySpan<byte> bytes, MemoryAllocator<char>? allocator = null, bool flush = false)
    {
        if (bytes.Length >= MaxInputSize)
            throw new ArgumentException(ExceptionMessages.LargeBuffer, nameof(bytes));

        var result = new MemoryOwnerWrapper<char>(allocator);
        EncodeToChars(bytes, ref result, flush);
        return result.Buffer;
    }

    [SkipLocalsInit]
    private void EncodeToCharsCore<TConsumer>(ReadOnlySpan<byte> bytes, TConsumer output, bool flush)
        where TConsumer : notnull, IReadOnlySpanConsumer<char>
    {
        Span<char> buffer = stackalloc char[EncodingBufferSize];

    consume_next_chunk:
        var chunk = bytes.TrimLength(DecodingBufferSize);
        if (Encode(chunk, buffer, out var consumed, out var produced))
        {
            Reset();
        }
        else
        {
            reservedBufferSize = chunk.Length - consumed;
            Debug.Assert(reservedBufferSize <= MaxBufferedDataSize);
            chunk.Slice(consumed).CopyTo(ReservedBytes);
        }

        if (consumed > 0 && produced > 0)
        {
            output.Invoke(buffer.Slice(0, produced));
            bytes = bytes.Slice(consumed);
            goto consume_next_chunk;
        }

        // flush the rest of the buffer
        if (HasBufferedData && flush)
        {
            Convert.TryToBase64Chars(Span.AsReadOnlyBytes(in reservedBuffer).Slice(0, reservedBufferSize), buffer, out produced);
            Reset();
            output.Invoke(buffer.Slice(0, produced));
        }

        static bool Encode(ReadOnlySpan<byte> input, Span<char> output, out int consumedBytes, out int producedChars)
        {
            Debug.Assert(input.Length <= DecodingBufferSize);
            Debug.Assert(output.Length == EncodingBufferSize);

            bool result;
            int rest;

            if (result = (rest = input.Length % 3) is 0)
                consumedBytes = input.Length;
            else
                input = input.Slice(0, consumedBytes = input.Length - rest);

            return Convert.TryToBase64Chars(input, output, out producedChars) && result;
        }
    }

    [SkipLocalsInit]
    private void CopyAndEncodeToChars<TConsumer>(ReadOnlySpan<byte> bytes, TConsumer output, bool flush)
        where TConsumer : notnull, IReadOnlySpanConsumer<char>
    {
        var newSize = reservedBufferSize + bytes.Length;
        using var tempBuffer = (uint)newSize <= (uint)MemoryRental<char>.StackallocThreshold ? stackalloc byte[newSize] : new MemoryRental<byte>(newSize);
        ReservedBytes.Slice(0, reservedBufferSize).CopyTo(tempBuffer.Span);
        bytes.CopyTo(tempBuffer.Span.Slice(reservedBufferSize));
        EncodeToCharsCore(tempBuffer.Span, output, flush);
    }

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
    {
        if (HasBufferedData)
            CopyAndEncodeToChars(bytes, output, flush);
        else
            EncodeToCharsCore(bytes, output, flush);
    }

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
        => EncodeToChars(bytes, new DelegatingReadOnlySpanConsumer<char, TArg>(output, arg), flush);

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
        => EncodeToChars(bytes, new ReadOnlySpanConsumer<char, TArg>(output, arg), flush);

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
        => EncodeToChars<TextConsumer>(bytes, output, flush);

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
        => EncodeToChars<StringBuilderConsumer>(bytes, output, flush);

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
            const int bufferSize = ((MaxBufferedDataSize + 2) / 3) * 4;
            Span<byte> utf8Chars = stackalloc byte[bufferSize];
            charsWritten = Flush(utf8Chars);
            Utf8.ToUtf16(utf8Chars.Slice(0, charsWritten), output, out _, out charsWritten);
        }

        return charsWritten;
    }
}
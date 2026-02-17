using System.Buffers;
using System.ComponentModel;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNext.IO;

using Buffers;
using Buffers.Binary;
using Numerics;
using EncodingContext = Text.EncodingContext;

/// <summary>
/// Providers a uniform way to encode the data.
/// </summary>
/// <seealso cref="IAsyncBinaryReader"/>
public interface IAsyncBinaryWriter : ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>
{
    /// <summary>
    /// Encodes formattable value as a set of bytes.
    /// </summary>
    /// <typeparam name="T">The type of formattable value.</typeparam>
    /// <param name="value">The value to be written as a sequence of bytes.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    ValueTask WriteAsync<T>(T value, CancellationToken token = default)
        where T : IBinaryFormattable<T>
    {
        return IBinaryFormattable<T>.TryFormat(value, Buffer.Span)
            ? AdvanceAsync(T.Size, token)
            : WriteSlowAsync(value, token);
    }

    private async ValueTask WriteSlowAsync<T>(T value, CancellationToken token = default)
        where T : IBinaryFormattable<T>
    {
        using var buffer = IBinaryFormattable<T>.Format(value);
        await WriteAsync(buffer.Memory, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes integer value in little-endian format.
    /// </summary>
    /// <typeparam name="T">The integer type.</typeparam>
    /// <param name="value">The value to be written in little-endian format.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    ValueTask WriteLittleEndianAsync<T>(T value, CancellationToken token = default)
        where T : IBinaryInteger<T>
    {
        return value.TryWriteLittleEndian(Buffer.Span, out var bytesWritten)
            ? AdvanceAsync(bytesWritten, token)
            : WriteLittleEndianSlowAsync(value, token);
    }

    private async ValueTask WriteLittleEndianSlowAsync<T>(T value, CancellationToken token)
        where T : IBinaryInteger<T>
    {
        using var buffer = MemoryAllocator<byte>.Default.AllocateExactly(Number.get_MaxByteCount<T>());
        value.TryWriteLittleEndian(buffer.Span, out var bytesWritten);
        await WriteAsync(buffer.Memory.Slice(0, bytesWritten), token).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes integer value in big-endian format.
    /// </summary>
    /// <typeparam name="T">The integer type.</typeparam>
    /// <param name="value">The value to be written in big-endian format.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    ValueTask WriteBigEndianAsync<T>(T value, CancellationToken token = default)
        where T : IBinaryInteger<T>
    {
        return value.TryWriteBigEndian(Buffer.Span, out var bytesWritten)
            ? AdvanceAsync(bytesWritten, token)
            : WriteBigEndianSlowAsync(value, token);
    }

    private async ValueTask WriteBigEndianSlowAsync<T>(T value, CancellationToken token)
        where T : IBinaryInteger<T>
    {
        using var buffer = MemoryAllocator<byte>.Default.AllocateExactly(Number.get_MaxByteCount<T>());
        value.TryWriteBigEndian(buffer.Span, out var bytesWritten);
        await WriteAsync(buffer.Memory.Slice(0, bytesWritten), token).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the buffer to modify.
    /// </summary>
    /// <seealso cref="AdvanceAsync(int, CancellationToken)"/>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    Memory<byte> Buffer { get; }

    /// <summary>
    /// Instructs the writer that the specified number of bytes of <see cref="Buffer"/> is modified.
    /// </summary>
    /// <param name="bytesWritten">The number of written bytes to <see cref="Buffer"/>.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bytesWritten"/> is negative.</exception>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    ValueTask AdvanceAsync(int bytesWritten, CancellationToken token = default);

    /// <summary>
    /// Encodes a block of memory, optionally prefixed with the length encoded as a sequence of bytes
    /// according to the specified format.
    /// </summary>
    /// <param name="input">A block of memory.</param>
    /// <param name="lengthFormat">Indicates how the length of the BLOB must be encoded; or <see langword="null"/> to prevent length encoding.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    ValueTask WriteAsync(ReadOnlyMemory<byte> input, LengthFormat? lengthFormat = null, CancellationToken token = default)
    {
        return lengthFormat.HasValue
            ? WriteAsync(input, lengthFormat.GetValueOrDefault(), token)
            : WriteAsync(input, token);
    }

    private async ValueTask WriteAsync(ReadOnlyMemory<byte> input, LengthFormat lengthFormat, CancellationToken token)
    {
        if (TryWriteLengthFast(input.Length, lengthFormat, out var bytesWritten))
        {
            await AdvanceAsync(bytesWritten, token).ConfigureAwait(false);
        }
        else
        {
            await WriteLengthSlowAsync(input.Length, lengthFormat, token).ConfigureAwait(false);
        }

        await WriteAsync(input, token).ConfigureAwait(false);
    }

    private async ValueTask WriteAsync(ReadOnlyMemory<byte> input, CancellationToken token)
    {
        for (int bytesWritten; !input.IsEmpty; input = input.Slice(bytesWritten))
        {
            bytesWritten = input.Span >> Buffer.Span;
            await AdvanceAsync(bytesWritten, token).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    ValueTask ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<byte> input, CancellationToken token)
        => WriteAsync(input, lengthFormat: null, token);

    /// <summary>
    /// Attempts to get synchronous writer.
    /// </summary>
    /// <returns>Synchronous writer wrapped by this asynchronous writer; or <see langword="null"/> if underlying I/O is fully asynchronous.</returns>
    IBufferWriter<byte>? TryGetBufferWriter() => null;

    /// <summary>
    /// Encodes a block of characters using the specified encoding.
    /// </summary>
    /// <param name="chars">The characters to encode.</param>
    /// <param name="context">The context describing encoding of characters.</param>
    /// <param name="lengthFormat">String length encoding format; or <see langword="null"/> to prevent encoding of string length.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The number of written bytes.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    async ValueTask<long> EncodeAsync(ReadOnlyMemory<char> chars, EncodingContext context, LengthFormat? lengthFormat = null, CancellationToken token = default)
    {
        long result;
        int bytesWritten;
        
        if (lengthFormat.HasValue)
        {
            var length = context.Encoding.GetByteCount(chars.Span);
            if (TryWriteLengthFast(length, lengthFormat.GetValueOrDefault(), out bytesWritten))
            {
                await AdvanceAsync(bytesWritten, token).ConfigureAwait(false);
            }
            else
            {
                bytesWritten = await WriteLengthSlowAsync(length, lengthFormat.GetValueOrDefault(), token).ConfigureAwait(false);
            }

            result = bytesWritten;
        }
        else
        {
            result = 0L;
        }

        var encoder = context.GetEncoder();
        for (int charsUsed, maxByteCount = context.Encoding.GetMaxByteCount(1);
             !chars.IsEmpty;
             chars = chars.Slice(charsUsed), result += bytesWritten)
        {
            var buffer = Buffer;
            var maxChars = buffer.Length / maxByteCount;

            if (maxChars is 0)
            {
                using var extraBuffer = MemoryAllocator<byte>.Default.AllocateExactly(maxByteCount);
                await EncodeSingleCharAsync(encoder, chars.Span, extraBuffer.Memory, out charsUsed, out bytesWritten, token)
                    .ConfigureAwait(false);
            }
            else
            {
                await EncodeFragmentAsync(encoder, chars.Span, buffer.Span, maxChars, out charsUsed, out bytesWritten, token)
                    .ConfigureAwait(false);
            }
        }

        return result;
    }

    private ValueTask EncodeSingleCharAsync(
        Encoder encoder,
        ReadOnlySpan<char> chars,
        Memory<byte> buffer,
        out int charsUsed,
        out int bytesWritten,
        CancellationToken token)
    {
        encoder.Convert(chars, buffer.Span, chars.Length is 1, out charsUsed, out bytesWritten, out _);
        return WriteAsync(buffer.Slice(0, bytesWritten), token);
    }

    private ValueTask EncodeFragmentAsync(
        Encoder encoder,
        ReadOnlySpan<char> chars,
        Span<byte> buffer,
        int maxChars,
        out int charsUsed,
        out int bytesWritten,
        CancellationToken token)
    {
        encoder.Convert(chars, buffer, chars.Length <= maxChars, out charsUsed, out bytesWritten, out _);
        return AdvanceAsync(bytesWritten, token);
    }

    private static int WriteLength(int length, LengthFormat lengthFormat, Span<byte> output)
    {
        var writer = new SpanWriter<byte>(output);
        return writer.WriteLength(length, lengthFormat);
    }

    private bool TryWriteLengthFast(int length, LengthFormat lengthFormat, out int bytesWritten)
    {
        var buffer = Buffer;

        var canBeDoneSynchronously = buffer.Length >= lengthFormat.MaxByteCount;
        bytesWritten = canBeDoneSynchronously
            ? WriteLength(length, lengthFormat, buffer.Span)
            : 0;

        return canBeDoneSynchronously;
    }

    private async ValueTask<int> WriteLengthSlowAsync(int length, LengthFormat lengthFormat, CancellationToken token)
    {
        using var buffer = MemoryAllocator<byte>.Default.AllocateExactly(lengthFormat.MaxByteCount);
        length = WriteLength(length, lengthFormat, buffer.Span);
        await WriteAsync(buffer.Memory.Slice(0, length), token).ConfigureAwait(false);
        return length;
    }

    /// <summary>
    /// Encodes formattable value as a set of characters using the specified encoding.
    /// </summary>
    /// <typeparam name="T">The type of formattable value.</typeparam>
    /// <param name="value">The value to be written as string.</param>
    /// <param name="context">The context describing encoding of characters.</param>
    /// <param name="lengthFormat">String length encoding format.</param>
    /// <param name="format">The format of the value.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="allocator">Characters buffer allocator.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The number of written bytes.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    ValueTask<long> FormatAsync<T>(T value, EncodingContext context, LengthFormat? lengthFormat = null, string? format = null, IFormatProvider? provider = null, MemoryAllocator<char>? allocator = null, CancellationToken token = default)
        where T : ISpanFormattable
        => EncodeAsync(value.ToString(format, provider).AsMemory(), context, lengthFormat, token);

    /// <summary>
    /// Converts the value to UTF-8 encoded characters.
    /// </summary>
    /// <typeparam name="T">The type of the value to convert.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="lengthFormat">String length encoding format.</param>
    /// <param name="format">The format of the value.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The number of written bytes.</returns>
    /// <exception cref="InternalBufferOverflowException">The internal buffer cannot place all UTF-8 bytes exposed by <paramref name="value"/>.</exception>
    ValueTask<int> FormatAsync<T>(T value, LengthFormat lengthFormat, string? format = null, IFormatProvider? provider = null,
        CancellationToken token = default)
        where T : IUtf8SpanFormattable
        => lengthFormat.HasFixedSize
            ? FormatFastAsync(value, lengthFormat, format, provider, token)
            : FormatSlowAsync(value, lengthFormat, format, provider, token);

    private async ValueTask<int> FormatFastAsync<T>(T value, LengthFormat lengthFormat, string? format, IFormatProvider? provider, CancellationToken token)
        where T : IUtf8SpanFormattable
    {
        var buffer =  Buffer;

        if (TryFormat(value, lengthFormat, format, provider, buffer.Span, out var bytesWritten))
        {
            await AdvanceAsync(bytesWritten, token).ConfigureAwait(false);
        }
        else
        {
            bytesWritten = await FormatSlowAsync(value, lengthFormat, format, provider, token).ConfigureAwait(false);
        }

        return bytesWritten;

        static bool TryFormat(T value, LengthFormat lengthFormat, string? format, IFormatProvider? provider, Span<byte> buffer, out int bytesWritten)
        {
            var lengthBuffer = buffer.TrimLength(lengthFormat.MaxByteCount, out var payload);

            var completed = value.TryFormat(payload, out bytesWritten, format, provider);
            bytesWritten = completed
                ? bytesWritten + WriteLength(bytesWritten, lengthFormat, lengthBuffer)
                : 0;

            return completed;
        }
    }

    private async ValueTask<int> FormatSlowAsync<T>(T value, LengthFormat? lengthFormat, string? format, IFormatProvider? provider, CancellationToken token)
        where T : IUtf8SpanFormattable
    {
        const int maxBufferSize = int.MaxValue / 2;
        for (var bufferSize = SpanOwner<byte>.StackallocThreshold; ; bufferSize = bufferSize <= maxBufferSize ? bufferSize << 1 : throw new InternalBufferOverflowException())
        {
            using var buffer = MemoryAllocator<byte>.Default.AllocateAtLeast(bufferSize);

            if (value.TryFormat(buffer.Span, out var bytesWritten, format, provider))
            {
                await WriteAsync(buffer.Memory.Slice(0, bytesWritten), lengthFormat, token).ConfigureAwait(false);
                return bytesWritten;
            }
        }
    }

    /// <summary>
    /// Writes the content from the specified stream.
    /// </summary>
    /// <param name="source">The stream to read from the source.</param>
    /// <param name="count">The number of bytes to read from the source.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    ValueTask CopyFromAsync(Stream source, long? count = null, CancellationToken token = default)
    {
        return source is null
            ? ValueTask.FromException(new ArgumentNullException(nameof(source)))
            : count.HasValue
            ? CopyFromAsync(source, count.GetValueOrDefault(), token)
            : CopyFromAsync(source, token);
    }

    private async ValueTask CopyFromAsync(Stream source, CancellationToken token)
    {
        for (int bytesWritten; (bytesWritten = await source.ReadAsync(Buffer, token).ConfigureAwait(false)) > 0;)
        {
            await AdvanceAsync(bytesWritten, token).ConfigureAwait(false);
        }
    }

    private async ValueTask CopyFromAsync(Stream source, long count, CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        for (int bytesWritten; count > 0L; count -= bytesWritten)
        {
            bytesWritten = await source.ReadAsync(Buffer, token).ConfigureAwait(false);

            if (bytesWritten <= 0)
                throw new EndOfStreamException();

            await AdvanceAsync(bytesWritten, token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Writes the content from the specified pipe.
    /// </summary>
    /// <param name="source">The pipe to read from.</param>
    /// <param name="count">The number of bytes to read from the source.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    ValueTask CopyFromAsync(PipeReader source, long? count = null, CancellationToken token = default)
        => count.HasValue
            ? Pipelines.PipeExtensions.CopyToAsync(source, this, count.GetValueOrDefault(), token)
            : Pipelines.PipeExtensions.CopyToAsync(source, this, token);

    /// <summary>
    /// Creates default implementation of binary writer for the stream.
    /// </summary>
    /// <remarks>
    /// It is recommended to use extension methods from <see cref="StreamExtensions"/> class
    /// for encoding data to the stream. This method is intended for situation
    /// when you need an object implementing <see cref="IAsyncBinaryWriter"/> interface.
    /// </remarks>
    /// <param name="output">The stream instance.</param>
    /// <param name="buffer">The buffer used for encoding binary data.</param>
    /// <returns>The stream writer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> is empty.</exception>
    public static IAsyncBinaryWriter Create(Stream output, Memory<byte> buffer)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.EnsureWritable(output);
        ArgumentException.ThrowIfEmpty(buffer);

        return new AsyncStreamBinaryAccessor(output, buffer);
    }

    internal static Stream CreateStream<TWriter>(TWriter writer)
        where TWriter : IAsyncBinaryWriter
        => Stream.CreateAsyncWritable(new Wrapper<TWriter>(writer));

    [StructLayout(LayoutKind.Auto)]
    private struct Wrapper<TWriter>(TWriter writer) : ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>, IFlushable
        where TWriter : IAsyncBinaryWriter
    {
        private TWriter writer = writer; // not readonly to avoid defensive copy
        
        ValueTask ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<byte> source, CancellationToken token)
            => writer.Invoke(source, token);

        void IFlushable.Flush()
        {
        }

        Task IFlushable.FlushAsync(CancellationToken token)
            => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;
    }
}

file static class LengthFormatExtensions
{
    internal static int GetMaxByteCount(this LengthFormat format) => format switch
    {
        LengthFormat.BigEndian or LengthFormat.LittleEndian => sizeof(int),
        LengthFormat.Compressed => Leb128<int>.MaxSizeInBytes,
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    internal static bool HasFixedSize(this LengthFormat format)
        => format is LengthFormat.BigEndian or LengthFormat.LittleEndian;
}
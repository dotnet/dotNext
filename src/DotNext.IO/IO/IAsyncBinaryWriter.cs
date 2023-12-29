using System.Buffers;
using System.ComponentModel;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.IO;

using Buffers;
using Buffers.Binary;
using EncodingContext = Text.EncodingContext;
using PipeBinaryWriter = Pipelines.PipeBinaryWriter;

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
        where T : notnull, IBinaryFormattable<T>
    {
        return IBinaryFormattable<T>.TryFormat(value, Buffer.Span)
            ? AdvanceAsync(T.Size, token)
            : WriteSlowAsync(value, token);
    }

    private async ValueTask WriteSlowAsync<T>(T value, CancellationToken token = default)
        where T : notnull, IBinaryFormattable<T>
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
        where T : notnull, IBinaryInteger<T>
    {
        return value.TryWriteLittleEndian(Buffer.Span, out var bytesWritten)
            ? AdvanceAsync(bytesWritten, token)
            : ValueTask.FromException(new InternalBufferOverflowException());
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
        where T : notnull, IBinaryInteger<T>
    {
        return value.TryWriteBigEndian(Buffer.Span, out var bytesWritten)
            ? AdvanceAsync(bytesWritten, token)
            : ValueTask.FromException(new InternalBufferOverflowException());
    }

    /// <summary>
    /// Gets buffer to modify.
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
    /// according with the specified format.
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
        await AdvanceAsync(WriteLength(input.Length, lengthFormat), token).ConfigureAwait(false);
        await WriteAsync(input, token).ConfigureAwait(false);
    }

    private async ValueTask WriteAsync(ReadOnlyMemory<byte> input, CancellationToken token)
    {
        for (int bytesWritten; !input.IsEmpty; input = input.Slice(bytesWritten))
        {
            input.Span.CopyTo(Buffer.Span, out bytesWritten);
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
            result = bytesWritten = WriteLength(context.Encoding.GetByteCount(chars.Span), lengthFormat.GetValueOrDefault());
            await AdvanceAsync(bytesWritten, token).ConfigureAwait(false);
        }
        else
        {
            result = 0L;
        }

        var encoder = context.GetEncoder();
        for (int charsUsed; !chars.IsEmpty; chars = chars.Slice(charsUsed), result += bytesWritten)
        {
            var buffer = Buffer;
            var maxChars = buffer.Length / context.Encoding.GetMaxByteCount(1);
            encoder.Convert(chars.Span.Slice(0, maxChars), buffer.Span, chars.Length <= maxChars, out charsUsed, out bytesWritten, out _);
            await AdvanceAsync(bytesWritten, token).ConfigureAwait(false);
        }

        return result;
    }

    private int WriteLength(int length, LengthFormat lengthFormat)
    {
        var writer = new SpanWriter<byte>(Buffer.Span);
        return writer.WriteLength(length, lengthFormat);
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
        where T : notnull, ISpanFormattable
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
    ValueTask<int> FormatAsync<T>(T value, LengthFormat? lengthFormat, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
        where T : notnull, IUtf8SpanFormattable
        => FormatAsync(this, value, lengthFormat, format, provider, token);

    internal static async ValueTask<int> FormatAsync<T>(IAsyncBinaryWriter writer, T value, LengthFormat? lengthFormat, string? format, IFormatProvider? provider, CancellationToken token)
        where T : notnull, IUtf8SpanFormattable
    {
        const int maxBufferSize = int.MaxValue / 2;
        for (var bufferSize = MemoryRental<byte>.StackallocThreshold; ; bufferSize = bufferSize <= maxBufferSize ? bufferSize << 1 : throw new InternalBufferOverflowException())
        {
            using var buffer = Memory.AllocateAtLeast<byte>(bufferSize);

            if (value.TryFormat(buffer.Span, out var bytesWritten, format, provider))
            {
                await writer.WriteAsync(buffer.Memory.Slice(0, bytesWritten), lengthFormat, token).ConfigureAwait(false);
                return bytesWritten;
            }
        }
    }

    /// <summary>
    /// Writes the content from the specified stream.
    /// </summary>
    /// <param name="input">The stream to read from.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    async ValueTask CopyFromAsync(Stream input, CancellationToken token = default)
    {
        for (int bytesWritten; (bytesWritten = await input.ReadAsync(Buffer, token).ConfigureAwait(false)) > 0;)
        {
            await AdvanceAsync(bytesWritten, token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Writes the content from the specified pipe.
    /// </summary>
    /// <param name="input">The pipe to read from.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    ValueTask CopyFromAsync(PipeReader input, CancellationToken token = default)
        => Pipelines.PipeExtensions.CopyToAsync(input, this, token);

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
        StreamExtensions.ThrowIfEmpty(buffer);

        return new AsyncStreamBinaryAccessor(output, buffer);
    }

    /// <summary>
    /// Creates default implementation of binary writer for the pipe.
    /// </summary>
    /// <param name="output">The stream instance.</param>
    /// <param name="bufferSize">The maximum numbers of bytes that can be buffered in the memory without flushing.</param>
    /// <returns>The binary writer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bufferSize"/> or is less than or equal to zero.</exception>
    public static IAsyncBinaryWriter Create(PipeWriter output, long bufferSize = 0L)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentOutOfRangeException.ThrowIfNegative(bufferSize);

        return new PipeBinaryWriter(output, bufferSize);
    }

    /// <summary>
    /// Creates default implementation of binary writer for the buffer writer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <returns>The binary writer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> is <see langword="null"/>.</exception>
    public static IAsyncBinaryWriter Create(IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        return new AsyncBufferWriter(writer);
    }

    internal static Stream GetStream<TWriter>(TWriter writer, out bool keepAlive)
        where TWriter : notnull, IAsyncBinaryWriter
    {
        if (keepAlive = typeof(TWriter) == typeof(AsyncStreamBinaryAccessor))
            return Unsafe.As<TWriter, AsyncStreamBinaryAccessor>(ref writer).Stream;

        if (typeof(TWriter) == typeof(PipeBinaryWriter))
            return Unsafe.As<TWriter, PipeBinaryWriter>(ref writer).AsStream();

        if (typeof(TWriter) == typeof(AsyncBufferWriter))
            return Unsafe.As<TWriter, AsyncBufferWriter>(ref writer).AsStream();

        return StreamSource.AsAsynchronousStream(new Wrapper<TWriter>(writer));
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct Wrapper<TWriter>(TWriter writer) : ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>, IFlushable
        where TWriter : notnull, IAsyncBinaryWriter
    {
        ValueTask ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<byte> source, CancellationToken token)
            => writer.Invoke(source, token);

        void IFlushable.Flush()
        {
        }

        Task IFlushable.FlushAsync(CancellationToken token)
            => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;
    }
}
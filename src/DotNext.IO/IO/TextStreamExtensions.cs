using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace DotNext.IO;

using Buffers;
using InterpolatedString = Text.InterpolatedString;

/// <summary>
/// Represents various extension methods for <see cref="TextWriter"/> and <see cref="TextReader"/> classes.
/// </summary>
public static class TextStreamExtensions
{
    /// <summary>
    /// Creates text writer backed by the char buffer writer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="provider">The object that controls formatting.</param>
    /// <param name="flush">The optional implementation of <see cref="TextWriter.Flush"/> method.</param>
    /// <param name="flushAsync">The optional implementation of <see cref="TextWriter.FlushAsync"/> method.</param>
    /// <typeparam name="TWriter">The type of the char buffer writer.</typeparam>
    /// <returns>The text writer backed by the buffer writer.</returns>
    public static TextWriter AsTextWriter<TWriter>(this TWriter writer, IFormatProvider? provider = null, Action<TWriter>? flush = null, Func<TWriter, CancellationToken, Task>? flushAsync = null)
        where TWriter : class, IBufferWriter<char>
    {
        IFlushable.DiscoverFlushMethods(writer, ref flush, ref flushAsync);
        return new CharBufferWriter<TWriter>(writer, provider, flush, flushAsync);
    }

    /// <summary>
    /// Creates <see cref="TextReader"/> over the sequence of characters.
    /// </summary>
    /// <param name="sequence">The sequence of characters.</param>
    /// <returns>The reader over the sequence of characters.</returns>
    public static TextReader AsTextReader(this ReadOnlySequence<char> sequence)
        => new CharBufferReader(sequence);

    /// <summary>
    /// Creates <see cref="TextReader"/> over the sequence of encoded characters.
    /// </summary>
    /// <param name="sequence">The sequence of bytes representing encoded characters.</param>
    /// <param name="encoding">The encoding of the characters in the sequence.</param>
    /// <param name="bufferSize">The size of the internal <see cref="char"/> buffer used to decode characters.</param>
    /// <param name="allocator">The allocator of the internal buffer.</param>
    /// <returns>The reader over the sequence of encoded characters.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="encoding"/> is <see langword="null"/>; or <paramref name="bufferSize"/> is less than or equal to zero.</exception>
    public static TextReader AsTextReader(this ReadOnlySequence<byte> sequence, Encoding encoding, int bufferSize = 1024, MemoryAllocator<char>? allocator = null)
        => new DecodingTextReader(sequence, encoding, bufferSize, allocator);

    /// <summary>
    /// Asynchronously writes a linked regions of characters to the text stream.
    /// </summary>
    /// <param name="writer">The stream to write into.</param>
    /// <param name="chars">The linked regions of characters.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask WriteAsync(this TextWriter writer, ReadOnlySequence<char> chars, CancellationToken token = default)
    {
        foreach (var segment in chars)
            await writer.WriteAsync(segment, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates text writer backed by the byte buffer writer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="encoding">The encoding used to converts chars to bytes.</param>
    /// <param name="provider">The object that controls formatting.</param>
    /// <param name="flush">The optional implementation of <see cref="TextWriter.Flush"/> method.</param>
    /// <param name="flushAsync">The optional implementation of <see cref="TextWriter.FlushAsync"/> method.</param>
    /// <typeparam name="TWriter">The type of the char buffer writer.</typeparam>
    /// <returns>The text writer backed by the buffer writer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="encoding"/> is <see langword="null"/>.</exception>
    public static TextWriter AsTextWriter<TWriter>(this TWriter writer, Encoding encoding, IFormatProvider? provider = null, Action<TWriter>? flush = null, Func<TWriter, CancellationToken, Task>? flushAsync = null)
        where TWriter : class, IBufferWriter<byte>
    {
        IFlushable.DiscoverFlushMethods(writer, ref flush, ref flushAsync);
        return new EncodingTextWriter<TWriter>(writer, encoding, provider, flush, flushAsync);
    }

    /// <summary>
    /// Writes interpolated string.
    /// </summary>
    /// <param name="writer">An output for the interpolated string.</param>
    /// <param name="allocator">The allocator for the buffer used by string interpolation handler.</param>
    /// <param name="provider">Optional formatting provider to be applied for each interpolated string argument.</param>
    /// <param name="handler">The interpolated string handler.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    public static ValueTask WriteAsync(this TextWriter writer, MemoryAllocator<char>? allocator, IFormatProvider? provider, [InterpolatedStringHandlerArgument(nameof(allocator), nameof(provider))] ref PoolingInterpolatedStringHandler handler, CancellationToken token = default)
    {
        return WriteAsync(writer, InterpolatedString.Allocate(allocator, provider, ref handler), token);

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        static async ValueTask WriteAsync(TextWriter writer, MemoryOwner<char> buffer, CancellationToken token)
        {
            try
            {
                await writer.WriteAsync(buffer.Memory, token).ConfigureAwait(false);
            }
            finally
            {
                buffer.Dispose();
            }
        }
    }

    /// <summary>
    /// Writes interpolated string.
    /// </summary>
    /// <param name="writer">An output for the interpolated string.</param>
    /// <param name="allocator">The allocator for the buffer used by string interpolation handler.</param>
    /// <param name="handler">The interpolated string handler.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    public static ValueTask WriteAsync(this TextWriter writer, MemoryAllocator<char>? allocator, [InterpolatedStringHandlerArgument(nameof(allocator))] ref PoolingInterpolatedStringHandler handler, CancellationToken token = default)
        => WriteAsync(writer, allocator, null, ref handler, token);

    /// <summary>
    /// Writes interpolated string and appends a new line after it.
    /// </summary>
    /// <param name="writer">An output for the interpolated string.</param>
    /// <param name="allocator">The allocator for the buffer used by string interpolation handler.</param>
    /// <param name="provider">Optional formatting provider to be applied for each interpolated string argument.</param>
    /// <param name="handler">The interpolated string handler.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    public static ValueTask WriteLineAsync(this TextWriter writer, MemoryAllocator<char>? allocator, IFormatProvider? provider, [InterpolatedStringHandlerArgument(nameof(allocator), nameof(provider))] ref PoolingInterpolatedStringHandler handler, CancellationToken token = default)
    {
        handler.AppendLiteral(Environment.NewLine);
        return WriteAsync(writer, allocator, provider, ref handler, token);
    }

    /// <summary>
    /// Writes interpolated string and appends a new line after it.
    /// </summary>
    /// <param name="writer">An output for the interpolated string.</param>
    /// <param name="allocator">The allocator for the buffer used by string interpolation handler.</param>
    /// <param name="handler">The interpolated string handler.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    public static ValueTask WriteLineAsync(this TextWriter writer, MemoryAllocator<char>? allocator, [InterpolatedStringHandlerArgument(nameof(allocator))] ref PoolingInterpolatedStringHandler handler, CancellationToken token = default)
        => WriteLineAsync(writer, allocator, null, ref handler, token);

    /// <summary>
    /// Writes interpolated string and appends a new line after it.
    /// </summary>
    /// <param name="writer">An output for the interpolated string.</param>
    /// <param name="allocator">The allocator for the buffer used by string interpolation handler.</param>
    /// <param name="provider">Optional formatting provider to be applied for each interpolated string argument.</param>
    /// <param name="handler">The interpolated string handler.</param>
    public static void Write(this TextWriter writer, MemoryAllocator<char>? allocator, IFormatProvider? provider, [InterpolatedStringHandlerArgument(nameof(allocator))] ref PoolingInterpolatedStringHandler handler)
    {
        using var buffer = InterpolatedString.Allocate(allocator, provider, ref handler);
        writer.Write(buffer.Span);
    }

    /// <summary>
    /// Writes interpolated string and appends a new line after it.
    /// </summary>
    /// <param name="writer">An output for the interpolated string.</param>
    /// <param name="allocator">The allocator for the buffer used by string interpolation handler.</param>
    /// <param name="handler">The interpolated string handler.</param>
    public static void Write(this TextWriter writer, MemoryAllocator<char>? allocator, [InterpolatedStringHandlerArgument(nameof(allocator))] ref PoolingInterpolatedStringHandler handler)
        => Write(writer, allocator, null, ref handler);

    /// <summary>
    /// Writes interpolated string and appends a new line after it.
    /// </summary>
    /// <param name="writer">An output for the interpolated string.</param>
    /// <param name="allocator">The allocator for the buffer used by string interpolation handler.</param>
    /// <param name="provider">Optional formatting provider to be applied for each interpolated string argument.</param>
    /// <param name="handler">The interpolated string handler.</param>
    public static void WriteLine(this TextWriter writer, MemoryAllocator<char>? allocator, IFormatProvider? provider, [InterpolatedStringHandlerArgument(nameof(allocator))] ref PoolingInterpolatedStringHandler handler)
    {
        handler.AppendLiteral(Environment.NewLine);
        using var buffer = InterpolatedString.Allocate(allocator, provider, ref handler);
        writer.Write(buffer.Span);
    }

    /// <summary>
    /// Writes interpolated string and appends a new line after it.
    /// </summary>
    /// <param name="writer">An output for the interpolated string.</param>
    /// <param name="allocator">The allocator for the buffer used by string interpolation handler.</param>
    /// <param name="handler">The interpolated string handler.</param>
    public static void WriteLine(this TextWriter writer, MemoryAllocator<char>? allocator, [InterpolatedStringHandlerArgument(nameof(allocator))] ref PoolingInterpolatedStringHandler handler)
        => WriteLine(writer, allocator, null, ref handler);

    /// <summary>
    /// Reads text stream sequentially.
    /// </summary>
    /// <remarks>
    /// The returned memory block should not be used between iterations.
    /// </remarks>
    /// <param name="reader">Readable stream.</param>
    /// <param name="bufferSize">The buffer size.</param>
    /// <param name="allocator">The allocator of the buffer.</param>
    /// <param name="token">The token that can be used to cancel the enumeration.</param>
    /// <returns>A collection of memort blocks that can be obtained sequentially to read a whole stream.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bufferSize"/> is less than 1.</exception>
    public static async IAsyncEnumerable<ReadOnlyMemory<char>> ReadAllAsync(this TextReader reader, int bufferSize, MemoryAllocator<char>? allocator = null, [EnumeratorCancellation] CancellationToken token = default)
    {
        if (bufferSize < 1)
            throw new ArgumentOutOfRangeException(nameof(bufferSize));

        using var bufferOwner = allocator.Invoke(bufferSize, exactSize: false);
        var buffer = bufferOwner.Memory;

        for (int count; (count = await reader.ReadAsync(buffer, token).ConfigureAwait(false)) > 0;)
            yield return buffer.Slice(0, count);
    }
}
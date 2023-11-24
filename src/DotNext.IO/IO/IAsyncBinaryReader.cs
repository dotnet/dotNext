using System.Buffers;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.InteropServices;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.IO;

using Buffers;
using DecodingContext = Text.DecodingContext;
using PipeConsumer = Pipelines.PipeConsumer;
using static Text.EncodingExtensions;

/// <summary>
/// Providers a uniform way to decode the data
/// from various sources such as streams, pipes, unmanaged memory etc.
/// </summary>
/// <seealso cref="IAsyncBinaryWriter"/>
public interface IAsyncBinaryReader
{
    /// <summary>
    /// Represents empty reader.
    /// </summary>
    public static IAsyncBinaryReader Empty => EmptyBinaryReader.Instance;

    /// <summary>
    /// Decodes the value of blittable type.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <returns>The decoded value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    async ValueTask<T> ReadAsync<T>(CancellationToken token = default)
        where T : unmanaged
    {
        using var buffer = MemoryAllocator.Allocate<byte>(Unsafe.SizeOf<T>(), true);
        await ReadAsync(buffer.Memory, token).ConfigureAwait(false);
        return MemoryMarshal.Read<T>(buffer.Span);
    }

    /// <summary>
    /// Decodes 64-bit signed integer using the specified endianness.
    /// </summary>
    /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    async ValueTask<long> ReadInt64Async(bool littleEndian, CancellationToken token = default)
    {
        var result = await ReadAsync<long>(token).ConfigureAwait(false);
        result.ReverseIfNeeded(littleEndian);
        return result;
    }

    /// <summary>
    /// Decodes 32-bit signed integer using the specified endianness.
    /// </summary>
    /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    async ValueTask<int> ReadInt32Async(bool littleEndian, CancellationToken token = default)
    {
        var result = await ReadAsync<int>(token).ConfigureAwait(false);
        result.ReverseIfNeeded(littleEndian);
        return result;
    }

    /// <summary>
    /// Decodes 16-bit signed integer using the specified endianness.
    /// </summary>
    /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    async ValueTask<short> ReadInt16Async(bool littleEndian, CancellationToken token = default)
    {
        var result = await ReadAsync<short>(token).ConfigureAwait(false);
        result.ReverseIfNeeded(littleEndian);
        return result;
    }

    /// <summary>
    /// Parses the value encoded as a set of characters.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="parser">The parser.</param>
    /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
    /// <param name="context">The decoding context containing string characters encoding.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="FormatException">The string is in wrong format.</exception>
    async ValueTask<T> ParseAsync<T>(Parser<T> parser, LengthFormat lengthFormat, DecodingContext context, IFormatProvider? provider = null, CancellationToken token = default)
        where T : notnull
        => parser(await ReadStringAsync(lengthFormat, context, token).ConfigureAwait(false), provider);

    /// <summary>
    /// Parses the value encoded as a sequence of bytes.
    /// </summary>
    /// <typeparam name="T">The formattable type.</typeparam>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    async ValueTask<T> ParseAsync<T>(CancellationToken token = default)
        where T : notnull, IBinaryFormattable<T>
    {
        using var buffer = MemoryAllocator.Allocate<byte>(T.Size, true);
        await ReadAsync(buffer.Memory, token).ConfigureAwait(false);
        return IBinaryFormattable<T>.Parse(buffer.Span);
    }

    /// <summary>
    /// Decodes an arbitrary integer value.
    /// </summary>
    /// <param name="length">The length of the value, in bytes.</param>
    /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    ValueTask<BigInteger> ReadBigIntegerAsync(int length, bool littleEndian, CancellationToken token = default)
    {
        return length switch
        {
            < 0 => ValueTask.FromException<BigInteger>(new ArgumentOutOfRangeException(nameof(length))),
            0 => new(BigInteger.Zero),
            _ => ReadAsync(),
        };

        async ValueTask<BigInteger> ReadAsync()
        {
            using var buffer = MemoryAllocator.Allocate<byte>(length, exactSize: true);
            await this.ReadAsync(buffer.Memory, token).ConfigureAwait(false);
            return new(buffer.Span, isBigEndian: !littleEndian);
        }
    }

    /// <summary>
    /// Decodes an arbitrary integer value.
    /// </summary>
    /// <param name="lengthFormat">The format of the value length encoded in the underlying stream.</param>
    /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    async ValueTask<BigInteger> ReadBigIntegerAsync(LengthFormat lengthFormat, bool littleEndian, CancellationToken token = default)
    {
        using var buffer = await ReadAsync(lengthFormat, token: token).ConfigureAwait(false);
        return buffer.IsEmpty ? BigInteger.Zero : new(buffer.Span, isBigEndian: !littleEndian);
    }

    /// <summary>
    /// Reads the block of bytes.
    /// </summary>
    /// <param name="output">The block of memory to fill.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    ValueTask ReadAsync(Memory<byte> output, CancellationToken token = default);

    /// <summary>
    /// Skips the block of bytes.
    /// </summary>
    /// <param name="length">The length of the block to skip, in bytes.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
    ValueTask SkipAsync(long length, CancellationToken token = default)
    {
        return length switch
        {
            < 0L => ValueTask.FromException(new ArgumentOutOfRangeException(nameof(length))),
            0L => ValueTask.CompletedTask,
            _ => SkipAsync(),
        };

        async ValueTask SkipAsync()
        {
            const int tempBufferSize = 4096;

            // fixed-size buffer to avoid OOM
            using var buffer = MemoryAllocator.Allocate<byte>((int)Math.Min(tempBufferSize, length), exactSize: false);
            for (var bytesToRead = buffer.Length; length > 0L; length -= bytesToRead)
            {
                bytesToRead = (int)Math.Min(bytesToRead, length);
                var block = buffer.Memory.Slice(0, bytesToRead);
                await ReadAsync(block, token).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Reads length-prefixed block of bytes.
    /// </summary>
    /// <param name="lengthFormat">The format of the block length encoded in the underlying stream.</param>
    /// <param name="allocator">The memory allocator used to place the decoded block of bytes.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The decoded block of bytes.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    ValueTask<MemoryOwner<byte>> ReadAsync(LengthFormat lengthFormat, MemoryAllocator<byte>? allocator = null, CancellationToken token = default);

    /// <summary>
    /// Decodes the string.
    /// </summary>
    /// <param name="length">The length of the encoded string, in bytes.</param>
    /// <param name="context">The decoding context containing string characters encoding.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The decoded string.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    ValueTask<string> ReadStringAsync(int length, DecodingContext context, CancellationToken token = default)
    {
        return length switch
        {
            < 0 => ValueTask.FromException<string>(new ArgumentOutOfRangeException(nameof(length))),
            0 => new(string.Empty),
            _ => ReadAsync(),
        };

        async ValueTask<string> ReadAsync()
        {
            using var buffer = MemoryAllocator.Allocate<byte>(length, exactSize: true);
            await this.ReadAsync(buffer.Memory, token).ConfigureAwait(false);
            return context.Encoding.GetString(buffer.Span);
        }
    }

    /// <summary>
    /// Decodes the string.
    /// </summary>
    /// <param name="length">The length of the encoded string, in bytes.</param>
    /// <param name="context">The decoding context containing string characters encoding.</param>
    /// <param name="allocator">The allocator of the buffer of characters.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The buffer of characters.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    ValueTask<MemoryOwner<char>> ReadStringAsync(int length, DecodingContext context, MemoryAllocator<char>? allocator, CancellationToken token = default)
    {
        return length switch
        {
            < 0 => ValueTask.FromException<MemoryOwner<char>>(new ArgumentOutOfRangeException(nameof(length))),
            0 => new(default(MemoryOwner<char>)),
            _ => ReadAsync(),
        };

        async ValueTask<MemoryOwner<char>> ReadAsync()
        {
            using var buffer = MemoryAllocator.Allocate<byte>(length, exactSize: true);
            await this.ReadAsync(buffer.Memory, token).ConfigureAwait(false);
            return context.Encoding.GetChars(buffer.Span, allocator);
        }
    }

    /// <summary>
    /// Decodes the string.
    /// </summary>
    /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
    /// <param name="context">The decoding context containing string characters encoding.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The decoded string.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    async ValueTask<string> ReadStringAsync(LengthFormat lengthFormat, DecodingContext context, CancellationToken token = default)
    {
        using var buffer = await ReadAsync(lengthFormat, token: token).ConfigureAwait(false);
        return context.Encoding.GetString(buffer.Span);
    }

    /// <summary>
    /// Decodes the string.
    /// </summary>
    /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
    /// <param name="context">The decoding context containing string characters encoding.</param>
    /// <param name="allocator">The allocator of the buffer of characters.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The buffer of characters.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    async ValueTask<MemoryOwner<char>> ReadStringAsync(LengthFormat lengthFormat, DecodingContext context, MemoryAllocator<char>? allocator, CancellationToken token = default)
    {
        using var buffer = await ReadAsync(lengthFormat, token: token).ConfigureAwait(false);
        return context.Encoding.GetChars(buffer.Span, allocator);
    }

    /// <summary>
    /// Copies the content to the specified stream.
    /// </summary>
    /// <param name="output">The output stream receiving object content.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    Task CopyToAsync(Stream output, CancellationToken token = default)
        => CopyToAsync<StreamConsumer>(output, token);

    /// <summary>
    /// Copies the content to the specified pipe writer.
    /// </summary>
    /// <param name="output">The writer.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    Task CopyToAsync(PipeWriter output, CancellationToken token = default)
        => CopyToAsync<PipeConsumer>(output, token);

    /// <summary>
    /// Copies the content to the specified buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    Task CopyToAsync(IBufferWriter<byte> writer, CancellationToken token = default)
    {
        Task result;
        if (TryGetSequence(out var sequence))
        {
            result = Task.CompletedTask;
            try
            {
                foreach (var segment in sequence)
                {
                    writer.Write(segment.Span);
                    token.ThrowIfCancellationRequested();
                }
            }
            catch (OperationCanceledException e)
            {
                result = Task.FromCanceled(e.CancellationToken);
            }
            catch (Exception e)
            {
                result = Task.FromException(e);
            }
        }
        else
        {
            result = CopyToAsync(new BufferConsumer<byte>(writer), token);
        }

        return result;
    }

    /// <summary>
    /// Reads the entire content using the specified delegate.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument to be passed to the content reader.</typeparam>
    /// <param name="consumer">The content reader.</param>
    /// <param name="arg">The argument to be passed to the content reader.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    Task CopyToAsync<TArg>(ReadOnlySpanAction<byte, TArg> consumer, TArg arg, CancellationToken token = default)
    {
        Task result;
        if (TryGetSequence(out var sequence))
        {
            result = Task.CompletedTask;
            try
            {
                foreach (var segment in sequence)
                {
                    consumer(segment.Span, arg);
                    token.ThrowIfCancellationRequested();
                }
            }
            catch (OperationCanceledException e)
            {
                result = Task.FromCanceled(e.CancellationToken);
            }
            catch (Exception e)
            {
                result = Task.FromException(e);
            }
        }
        else
        {
            result = CopyToAsync(new DelegatingReadOnlySpanConsumer<byte, TArg>(consumer, arg), token);
        }

        return result;
    }

    /// <summary>
    /// Reads the entire content using the specified delegate.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument to be passed to the content reader.</typeparam>
    /// <param name="consumer">The content reader.</param>
    /// <param name="arg">The argument to be passed to the content reader.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    Task CopyToAsync<TArg>(Func<TArg, ReadOnlyMemory<byte>, CancellationToken, ValueTask> consumer, TArg arg, CancellationToken token = default)
        => CopyToAsync(new DelegatingMemoryConsumer<byte, TArg>(consumer, arg), token);

    /// <summary>
    /// Reads the entire content using the specified delegate.
    /// </summary>
    /// <param name="consumer">The content reader.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    Task CopyToAsync<TConsumer>(TConsumer consumer, CancellationToken token = default)
        where TConsumer : notnull, ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>;

    /// <summary>
    /// Attempts to get the entire content represented by this reader.
    /// </summary>
    /// <remarks>
    /// This method can be used for efficient synchronous decoding.
    /// </remarks>
    /// <param name="bytes">The content represented by this reader.</param>
    /// <returns><see langword="true"/> if the content is available synchronously; otherwise, <see langword="false"/>.</returns>
    bool TryGetSequence(out ReadOnlySequence<byte> bytes)
    {
        bytes = default;
        return false;
    }

    /// <summary>
    /// Attempts to get the number of bytes available for read.
    /// </summary>
    /// <param name="count">The number of bytes available for read.</param>
    /// <returns><see langword="true"/> if the method is supported; otherwise, <see langword="false"/>.</returns>
    bool TryGetRemainingBytesCount(out long count)
    {
        count = default;
        return false;
    }

    /// <summary>
    /// Creates default implementation of binary reader for the stream.
    /// </summary>
    /// <remarks>
    /// It is recommended to use extension methods from <see cref="StreamExtensions"/> class
    /// for decoding data from the stream. This method is intended for situation
    /// when you need an object implementing <see cref="IAsyncBinaryReader"/> interface.
    /// </remarks>
    /// <param name="input">The stream to be wrapped into the reader.</param>
    /// <param name="buffer">The buffer used for decoding data from the stream.</param>
    /// <returns>The stream reader.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> is empty.</exception>
    public static IAsyncBinaryReader Create(Stream input, Memory<byte> buffer)
        => ReferenceEquals(input, Stream.Null) ? Empty : new AsyncStreamBinaryAccessor(input, buffer);

    /// <summary>
    /// Creates default implementation of binary reader over sequence of bytes.
    /// </summary>
    /// <param name="sequence">The sequence of bytes.</param>
    /// <returns>The binary reader for the sequence of bytes.</returns>
    public static SequenceReader Create(ReadOnlySequence<byte> sequence) => new(sequence);

    /// <summary>
    /// Creates default implementation of binary reader over contiguous memory block.
    /// </summary>
    /// <param name="memory">The block of memory.</param>
    /// <returns>The binary reader for the memory block.</returns>
    public static SequenceReader Create(ReadOnlyMemory<byte> memory) => new(memory);

    /// <summary>
    /// Creates default implementation of binary reader for the specifed pipe reader.
    /// </summary>
    /// <remarks>
    /// It is recommended to use extension methods from <see cref="Pipelines.PipeExtensions"/> class
    /// for decoding data from the stream. This method is intended for situation
    /// when you need an object implementing <see cref="IAsyncBinaryReader"/> interface.
    /// </remarks>
    /// <param name="reader">The pipe reader.</param>
    /// <returns>The binary reader.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>.</exception>
    public static IAsyncBinaryReader Create(PipeReader reader) => new Pipelines.PipeBinaryReader(reader);
}
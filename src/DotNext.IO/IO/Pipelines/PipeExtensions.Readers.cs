using System.Buffers;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using static System.Buffers.Binary.BinaryPrimitives;
using Missing = System.Reflection.Missing;

namespace DotNext.IO.Pipelines;

using Buffers;
using Text;
using static Buffers.BufferReader;

/// <summary>
/// Represents extension method for parsing data stored in pipe.
/// </summary>
public static partial class PipeExtensions
{
    [StructLayout(LayoutKind.Auto)]
    private struct IncrementalHashBuilder : IBufferReader<Missing>
    {
        private readonly IncrementalHash hash;
        private readonly bool limited;
        private int remainingBytes;

        internal IncrementalHashBuilder(IncrementalHash hash, int? count)
        {
            this.hash = hash;
            if (count.HasValue)
            {
                limited = true;
                remainingBytes = count.GetValueOrDefault();
            }
            else
            {
                limited = false;
                remainingBytes = int.MaxValue;
            }
        }

        readonly int IBufferReader<Missing>.RemainingBytes => remainingBytes;

        readonly Missing IBufferReader<Missing>.Complete() => Missing.Value;

        void IBufferReader<Missing>.EndOfStream()
            => remainingBytes = limited ? throw new EndOfStreamException() : 0;

        void IBufferReader<Missing>.Append(scoped ReadOnlySpan<byte> block, scoped ref int consumedBytes)
        {
            hash.AppendData(block);
            if (limited)
                remainingBytes -= block.Length;
        }
    }

    private static async ValueTask<TResult> ReadAsync<TResult, TParser>(this PipeReader reader, TParser parser, CancellationToken token)
        where TParser : struct, IBufferReader<TResult>
    {
        var completed = false;
        for (SequencePosition consumed; parser.RemainingBytes > 0 && !completed; reader.AdvanceTo(consumed))
        {
            var readResult = await reader.ReadAsync(token).ConfigureAwait(false);
            readResult.ThrowIfCancellationRequested(reader, token);
            consumed = parser.Append<TResult, TParser>(readResult.Buffer);
            completed = readResult.IsCompleted;
        }

        if (parser.RemainingBytes > 0)
            parser.EndOfStream();

        return parser.Complete();
    }

    /// <summary>
    /// Parses the value encoded as a set of characters.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="parser">The parser.</param>
    /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
    /// <param name="context">The decoding context containing string characters encoding.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="FormatException">The string is in wrong format.</exception>
    public static async ValueTask<T> ParseAsync<T>(this PipeReader reader, Parser<T> parser, LengthFormat lengthFormat, DecodingContext context, IFormatProvider? provider = null, CancellationToken token = default)
        where T : notnull
    {
        var length = await ReadLengthAsync(reader, lengthFormat, token).ConfigureAwait(false);
        if (length == 0)
            throw new EndOfStreamException();

        using var buffer = new ArrayBuffer<char>(length);
        var bufferReader = new StringReader<ArrayBuffer<char>>(in context, buffer);
        var completed = false;

        for (SequencePosition consumed; bufferReader.RemainingBytes > 0 & !completed; reader.AdvanceTo(consumed))
        {
            var readResult = await reader.ReadAsync(token).ConfigureAwait(false);
            readResult.ThrowIfCancellationRequested(reader, token);
            consumed = bufferReader.Append<string, StringReader<ArrayBuffer<char>>>(readResult.Buffer);
            completed = readResult.IsCompleted;
        }

        return bufferReader.RemainingBytes == 0 ? parser(bufferReader.Complete(), provider) : throw new EndOfStreamException();
    }

    /// <summary>
    /// Parses the value encoded as a sequence of bytes.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    public static ValueTask<T> ParseAsync<T>(this PipeReader reader, CancellationToken token = default)
        where T : notnull, IBinaryFormattable<T>
    {
        ValueTask<T> result;
        if (TryReadExactly(reader, T.Size, out var readResult))
        {
            result = readResult.IsCanceled
                ? ValueTask.FromCanceled<T>(token.IsCancellationRequested ? token : new(true))
                : new(IBinaryFormattable<T>.Parse(readResult.Buffer));

            reader.AdvanceTo(readResult.Buffer.GetPosition(T.Size));
        }
        else
        {
            result = ParseSlowAsync(reader, token);
        }

        return result;

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        static async ValueTask<T> ParseSlowAsync(PipeReader reader, CancellationToken token)
        {
            using var buffer = MemoryAllocator.AllocateExactly<byte>(T.Size);
            await ReadExactlyAsync(reader, buffer.Memory, token).ConfigureAwait(false);
            return IBinaryFormattable<T>.Parse(buffer.Span);
        }
    }

    private static async ValueTask<int> ComputeHashAsync(PipeReader reader, HashAlgorithmName name, int? count, Memory<byte> output, CancellationToken token)
    {
        using var hash = IncrementalHash.CreateHash(name);
        await reader.ReadAsync<Missing, IncrementalHashBuilder>(new IncrementalHashBuilder(hash, count), token).ConfigureAwait(false);
        if (!hash.TryGetCurrentHash(output.Span, out var bytesWritten))
            throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(output));
        return bytesWritten;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ValueTask<int> Read7BitEncodedIntAsync(this PipeReader reader, CancellationToken token)
        => reader.ReadAsync<int, SevenBitEncodedIntReader>(new SevenBitEncodedIntReader(5), token);

    /// <summary>
    /// Computes the hash for the pipe.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="name">The name of the hash algorithm.</param>
    /// <param name="count">The number of bytes to be added to the hash.</param>
    /// <param name="output">The buffer used to write the final hash.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The length of the final hash.</returns>
    /// <exception cref="ArgumentException"><paramref name="output"/> is too small for the hash.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    public static ValueTask<int> ComputeHashAsync(this PipeReader reader, HashAlgorithmName name, int count, Memory<byte> output, CancellationToken token = default)
        => ComputeHashAsync(reader, name, new int?(count), output, token);

    /// <summary>
    /// Computes the hash for the pipe.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="name">The name of the hash algorithm.</param>
    /// <param name="output">The buffer used to write the final hash.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The length of the final hash.</returns>
    /// <exception cref="ArgumentException"><paramref name="output"/> is too small for the hash.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<int> ComputeHashAsync(this PipeReader reader, HashAlgorithmName name, Memory<byte> output, CancellationToken token = default)
        => ComputeHashAsync(reader, name, null, output, token);

    /// <summary>
    /// Decodes string asynchronously from pipe.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="length">The length of the string, in bytes.</param>
    /// <param name="context">The text decoding context.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The decoded string.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
    /// <exception cref="EndOfStreamException"><paramref name="reader"/> doesn't contain the necessary number of bytes to restore string.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask<string> ReadStringAsync(this PipeReader reader, int length, DecodingContext context, CancellationToken token = default)
    {
        using var chars = await ReadStringAsync(reader, length, context, null, token).ConfigureAwait(false);
        return chars.IsEmpty ? string.Empty : new string(chars.Span);
    }

    /// <summary>
    /// Decodes string asynchronously from pipe.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="length">The length of the string, in bytes.</param>
    /// <param name="context">The text decoding context.</param>
    /// <param name="allocator">The allocator of the buffer of characters.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The buffer of characters.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
    /// <exception cref="EndOfStreamException"><paramref name="reader"/> doesn't contain the necessary number of bytes to restore string.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask<MemoryOwner<char>> ReadStringAsync(this PipeReader reader, int length, DecodingContext context, MemoryAllocator<char>? allocator, CancellationToken token = default)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        MemoryOwner<char> result;

        if (length == 0)
        {
            result = default;
        }
        else
        {
            result = allocator.AllocateExactly<char>(length);
            length = await ReadAsync<int, StringReader<ArrayBuffer<char>>>(reader, new(context, new ArrayBuffer<char>(result)), token).ConfigureAwait(false);
            result.TryResize(length);
        }

        return result;
    }

    private static async ValueTask<int> ReadLengthAsync(this PipeReader reader, LengthFormat lengthFormat, CancellationToken token)
    {
        ValueTask<int> result;
        var littleEndian = BitConverter.IsLittleEndian;
        switch (lengthFormat)
        {
            default:
                throw new ArgumentOutOfRangeException(nameof(lengthFormat));
            case LengthFormat.Plain:
                result = reader.ReadAsync<int>(token);
                break;
            case LengthFormat.PlainLittleEndian:
                littleEndian = true;
                goto case LengthFormat.Plain;
            case LengthFormat.PlainBigEndian:
                littleEndian = false;
                goto case LengthFormat.Plain;
            case LengthFormat.Compressed:
                result = reader.Read7BitEncodedIntAsync(token);
                break;
        }

        var length = await result.ConfigureAwait(false);
        length.ReverseIfNeeded(littleEndian);
        return length;
    }

    /// <summary>
    /// Decodes an arbitrary large integer.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="length">The length of the value, in bytes.</param>
    /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="EndOfStreamException"><paramref name="reader"/> doesn't contain the necessary number of bytes to restore string.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask<BigInteger> ReadBigIntegerAsync(this PipeReader reader, int length, bool littleEndian, CancellationToken token = default)
    {
        if (length == 0)
            return BigInteger.Zero;
        using var resultBuffer = new ArrayBuffer<byte>(length);
        return await ReadAsync<BigInteger, BigIntegerReader<ArrayBuffer<byte>>>(reader, new BigIntegerReader<ArrayBuffer<byte>>(resultBuffer, littleEndian), token).ConfigureAwait(false);
    }

    /// <summary>
    /// Decodes an arbitrary large integer.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="lengthFormat">The format of the value length encoded in the underlying stream.</param>
    /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="EndOfStreamException"><paramref name="reader"/> doesn't contain the necessary number of bytes to restore string.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static async ValueTask<BigInteger> ReadBigIntegerAsync(this PipeReader reader, LengthFormat lengthFormat, bool littleEndian, CancellationToken token = default)
        => await ReadBigIntegerAsync(reader, await reader.ReadLengthAsync(lengthFormat, token).ConfigureAwait(false), littleEndian, token).ConfigureAwait(false);

    /// <summary>
    /// Decodes string asynchronously from pipe.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="lengthFormat">Represents string length encoding format.</param>
    /// <param name="context">The text decoding context.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The decoded string.</returns>
    /// <exception cref="EndOfStreamException"><paramref name="reader"/> doesn't contain the necessary number of bytes to restore string.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static async ValueTask<string> ReadStringAsync(this PipeReader reader, LengthFormat lengthFormat, DecodingContext context, CancellationToken token = default)
        => await ReadStringAsync(reader, await reader.ReadLengthAsync(lengthFormat, token).ConfigureAwait(false), context, token).ConfigureAwait(false);

    /// <summary>
    /// Decodes string asynchronously from pipe.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="lengthFormat">Represents string length encoding format.</param>
    /// <param name="context">The text decoding context.</param>
    /// <param name="allocator">The allocator of the buffer of characters.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The buffer of characters.</returns>
    /// <exception cref="EndOfStreamException"><paramref name="reader"/> doesn't contain the necessary number of bytes to restore string.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static async ValueTask<MemoryOwner<char>> ReadStringAsync(this PipeReader reader, LengthFormat lengthFormat, DecodingContext context, MemoryAllocator<char>? allocator, CancellationToken token = default)
        => await ReadStringAsync(reader, await reader.ReadLengthAsync(lengthFormat, token).ConfigureAwait(false), context, allocator, token).ConfigureAwait(false);

    /// <summary>
    /// Reads value of blittable type from pipe.
    /// </summary>
    /// <typeparam name="T">The blittable type to decode.</typeparam>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<T> ReadAsync<T>(this PipeReader reader, CancellationToken token = default)
        where T : unmanaged
    {
        ValueTask<T> result;

        if (!TryReadExactly(reader, Unsafe.SizeOf<T>(), out var readResult))
        {
            result = ReadSlowAsync(reader, token);
        }
        else if (readResult.IsCanceled)
        {
            reader.AdvanceTo(readResult.Buffer.Start);
            result = ValueTask.FromCanceled<T>(token.IsCancellationRequested ? token : new(true));
        }
        else
        {
            result = new(Read(readResult.Buffer, out var consumed));
            reader.AdvanceTo(consumed);
        }

        return result;

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        static async ValueTask<T> ReadSlowAsync(PipeReader reader, CancellationToken token)
        {
            var result = await reader.ReadAtLeastAsync(Unsafe.SizeOf<T>(), token).ConfigureAwait(false);
            result.ThrowIfCancellationRequested(reader, token);
            var consumed = result.Buffer.Start;
            T value;
            try
            {
                value = Read(result.Buffer, out consumed);
            }
            finally
            {
                reader.AdvanceTo(consumed);
            }

            return value;
        }

        [SkipLocalsInit]
        static unsafe T Read(ReadOnlySequence<byte> sequence, out SequencePosition consumed)
        {
            Unsafe.SkipInit(out T result);
            sequence.CopyTo(Span.AsBytes(ref result), out var count);
            consumed = sequence.GetPosition(count);
            return count == sizeof(T) ? result : throw new EndOfStreamException();
        }
    }

    private static async ValueTask<TOutput> ReadAsync<TInput, TOutput, TConverter>(this PipeReader reader, TConverter converter, CancellationToken token)
        where TInput : unmanaged
        where TOutput : unmanaged
        where TConverter : struct, ISupplier<TInput, TOutput>
        => converter.Invoke(await ReadAsync<TInput>(reader, token).ConfigureAwait(false));

    /// <summary>
    /// Reads the entire content using the specified delegate.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument to be passed to the content reader.</typeparam>
    /// <param name="reader">The pipe to read from.</param>
    /// <param name="consumer">The content reader.</param>
    /// <param name="arg">The argument to be passed to the content reader.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static Task CopyToAsync<TArg>(this PipeReader reader, ReadOnlySpanAction<byte, TArg> consumer, TArg arg, CancellationToken token = default)
        => CopyToAsync(reader, new DelegatingReadOnlySpanConsumer<byte, TArg>(consumer, arg), token);

    /// <summary>
    /// Reads the entire content using the specified consumer.
    /// </summary>
    /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
    /// <param name="reader">The pipe to read from.</param>
    /// <param name="consumer">The content reader.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async Task CopyToAsync<TConsumer>(this PipeReader reader, TConsumer consumer, CancellationToken token = default)
        where TConsumer : notnull, ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>
    {
        await foreach (var chunk in ReadAllAsync(reader, token).ConfigureAwait(false))
        {
            await consumer.Invoke(chunk, token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads the entire content using the specified delegate.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument to be passed to the content reader.</typeparam>
    /// <param name="reader">The pipe to read from.</param>
    /// <param name="consumer">The content reader.</param>
    /// <param name="arg">The argument to be passed to the content reader.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static Task CopyToAsync<TArg>(this PipeReader reader, Func<TArg, ReadOnlyMemory<byte>, CancellationToken, ValueTask> consumer, TArg arg, CancellationToken token = default)
        => CopyToAsync(reader, new DelegatingMemoryConsumer<byte, TArg>(consumer, arg), token);

    /// <summary>
    /// Decodes 64-bit signed integer using the specified endianness.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    public static unsafe ValueTask<long> ReadInt64Async(this PipeReader reader, bool littleEndian, CancellationToken token = default)
        => littleEndian == BitConverter.IsLittleEndian ? reader.ReadAsync<long>(token) : reader.ReadAsync<long, long, Supplier<long, long>>(new(&ReverseEndianness), token);

    /// <summary>
    /// Decodes 64-bit unsigned integer using the specified endianness.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    [CLSCompliant(false)]
    public static unsafe ValueTask<ulong> ReadUInt64Async(this PipeReader reader, bool littleEndian, CancellationToken token = default)
        => littleEndian == BitConverter.IsLittleEndian ? reader.ReadAsync<ulong>(token) : reader.ReadAsync<ulong, ulong, Supplier<ulong, ulong>>(new(&ReverseEndianness), token);

    /// <summary>
    /// Decodes 32-bit signed integer using the specified endianness.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    public static unsafe ValueTask<int> ReadInt32Async(this PipeReader reader, bool littleEndian, CancellationToken token = default)
        => littleEndian == BitConverter.IsLittleEndian ? reader.ReadAsync<int>(token) : reader.ReadAsync<int, int, Supplier<int, int>>(new(&ReverseEndianness), token);

    /// <summary>
    /// Decodes 32-bit unsigned integer using the specified endianness.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    [CLSCompliant(false)]
    public static unsafe ValueTask<uint> ReadUInt32Async(this PipeReader reader, bool littleEndian, CancellationToken token = default)
        => littleEndian == BitConverter.IsLittleEndian ? reader.ReadAsync<uint>(token) : reader.ReadAsync<uint, uint, Supplier<uint, uint>>(new(&ReverseEndianness), token);

    /// <summary>
    /// Decodes 16-bit signed integer using the specified endianness.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    public static unsafe ValueTask<short> ReadInt16Async(this PipeReader reader, bool littleEndian, CancellationToken token = default)
        => littleEndian == BitConverter.IsLittleEndian ? reader.ReadAsync<short>(token) : reader.ReadAsync<short, short, Supplier<short, short>>(new(&ReverseEndianness), token);

    /// <summary>
    /// Decodes 16-bit signed integer using the specified endianness.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    [CLSCompliant(false)]
    public static unsafe ValueTask<ushort> ReadUInt16Async(this PipeReader reader, bool littleEndian, CancellationToken token = default)
        => littleEndian == BitConverter.IsLittleEndian ? reader.ReadAsync<ushort>(token) : reader.ReadAsync<ushort, ushort, Supplier<ushort, ushort>>(new(&ReverseEndianness), token);

    /// <summary>
    /// Reads the block of memory.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="output">The block of memory to fill from the pipe.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The task representing asynchronous state of the operation.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">Reader doesn't have enough data.</exception>
    public static ValueTask ReadExactlyAsync(this PipeReader reader, Memory<byte> output, CancellationToken token = default)
    {
        if (output.IsEmpty)
            return ValueTask.CompletedTask;

        if (TryReadExactly(reader, output.Length, out var result))
        {
            result.Buffer.CopyTo(output.Span);
            reader.AdvanceTo(result.Buffer.GetPosition(output.Length));

            return result.IsCanceled ? ValueTask.FromCanceled(token.IsCancellationRequested ? token : new(true)) : ValueTask.CompletedTask;
        }

        return ReadBlockSlowAsync(reader, output, token);

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        static async ValueTask ReadBlockSlowAsync(PipeReader reader, Memory<byte> output, CancellationToken token)
        {
            var result = await reader.ReadAtLeastAsync(output.Length, token).ConfigureAwait(false);
            result.ThrowIfCancellationRequested(reader, token);
            Copy(reader, result.Buffer, output.Span);
        }

        static void Copy(PipeReader reader, ReadOnlySequence<byte> source, Span<byte> output)
        {
            if (source.Length < output.Length)
                throw new EndOfStreamException();

            var blockEnd = source.GetPosition(output.Length);
            source.Slice(source.Start, blockEnd).CopyTo(output);
            reader.AdvanceTo(blockEnd, source.End);
        }
    }

    /// <summary>
    /// Reads length-prefixed block of bytes.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="lengthFormat">The format of the block length encoded in the underlying pipe.</param>
    /// <param name="allocator">The memory allocator used to place the decoded block of bytes.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The decoded block of bytes.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">Reader doesn't have enough data.</exception>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public static async ValueTask<MemoryOwner<byte>> ReadBlockAsync(this PipeReader reader, LengthFormat lengthFormat, MemoryAllocator<byte>? allocator = null, CancellationToken token = default)
    {
        var length = await reader.ReadLengthAsync(lengthFormat, token).ConfigureAwait(false);
        MemoryOwner<byte> result;
        if (length > 0)
        {
            result = allocator.AllocateExactly(length);
            await ReadExactlyAsync(reader, result.Memory, token).ConfigureAwait(false);
        }
        else
        {
            result = default;
        }

        return result;
    }

    /// <summary>
    /// Attempts to read block of data synchronously.
    /// </summary>
    /// <remarks>
    /// This method doesn't advance the reader position.
    /// </remarks>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="length">The length of the block to consume, in bytes.</param>
    /// <param name="result">
    /// The requested block of data which length is equal to <paramref name="length"/> in case of success;
    /// otherwise, empty block.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the block of requested length is obtained successfully;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryReadExactly(this PipeReader reader, long length, out ReadResult result)
    {
        if (reader.TryRead(out result))
        {
            if (length <= result.Buffer.Length)
            {
                result = new(result.Buffer.Slice(0L, length), result.IsCanceled, result.IsCompleted);
                return true;
            }

            reader.AdvanceTo(result.Buffer.Start, result.Buffer.End);
        }

        return false;
    }

    /// <summary>
    /// Consumes the requested portion of data asynchronously.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="length">The length of the block to consume, in bytes.</param>
    /// <param name="consumer">The consumer of the memory block.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="TConsumer">The type of the memory block consumer.</typeparam>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">Reader doesn't have enough data.</exception>
    public static async ValueTask ReadBlockAsync<TConsumer>(this PipeReader reader, long length, TConsumer consumer, CancellationToken token = default)
        where TConsumer : notnull, ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>
    {
        if (length < 0L)
            throw new ArgumentOutOfRangeException(nameof(length));

        while (length > 0L)
        {
            var readResult = await reader.ReadAsync(token).ConfigureAwait(false);
            readResult.ThrowIfCancellationRequested(reader, token);
            var buffer = readResult.Buffer;
            var consumed = buffer.Start;
            try
            {
                if (buffer.IsEmpty)
                    throw new EndOfStreamException();

                for (int bytesToConsume; length > 0L && buffer.TryGet(ref consumed, out var block, false) && !block.IsEmpty; consumed = buffer.GetPosition(bytesToConsume, consumed), length -= bytesToConsume)
                {
                    bytesToConsume = Math.Min(block.Length, int.CreateSaturating(length));
                    await consumer.Invoke(block.Slice(0, bytesToConsume), token).ConfigureAwait(false);
                }
            }
            finally
            {
                reader.AdvanceTo(consumed);
            }
        }
    }

    /// <summary>
    /// Consumes the requested portion of data asynchronously.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="length">The length of the block to consume, in bytes.</param>
    /// <param name="callback">The callback to be called for each consumed segment.</param>
    /// <param name="arg">The argument to be passed to the callback.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="TArg">The type of the argument to be passed to the callback.</typeparam>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">Reader doesn't have enough data.</exception>
    public static ValueTask ReadBlockAsync<TArg>(this PipeReader reader, long length, Func<TArg, ReadOnlyMemory<byte>, CancellationToken, ValueTask> callback, TArg arg, CancellationToken token = default)
        => ReadBlockAsync(reader, length, new DelegatingMemoryConsumer<byte, TArg>(callback, arg), token);

    /// <summary>
    /// Drops the specified number of bytes from the pipe.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="length">The number of bytes to skip.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">Reader doesn't have enough data to skip.</exception>
    public static ValueTask SkipAsync(this PipeReader reader, long length, CancellationToken token = default)
    {
        return length switch
        {
            0L => new(),
            < 0L => ValueTask.FromException(new ArgumentOutOfRangeException(nameof(length))),
            _ => SkipCoreAsync(reader, length, token),
        };

        static async ValueTask SkipCoreAsync(PipeReader reader, long length, CancellationToken token)
        {
            while (length > 0L)
            {
                var readResult = await reader.ReadAsync(token).ConfigureAwait(false);
                readResult.ThrowIfCancellationRequested(reader, token);
                var buffer = readResult.Buffer;
                var bytesToConsume = 0L;

                try
                {
                    if (buffer.IsEmpty)
                        throw new EndOfStreamException();

                    bytesToConsume = Math.Min(buffer.Length, length);
                    length -= bytesToConsume;
                }
                finally
                {
                    reader.AdvanceTo(buffer.GetPosition(bytesToConsume));
                }
            }
        }
    }

    /// <summary>
    /// Reads the block of memory.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="output">The block of memory to fill from the pipe.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The actual number of copied bytes.</returns>
    public static ValueTask<int> CopyToAsync(this PipeReader reader, Memory<byte> output, CancellationToken token = default)
        => ReadAsync<int, MemoryReader>(reader, new MemoryReader(output), token);

    /// <summary>
    /// Copies the data from the pipe to the buffer.
    /// </summary>
    /// <param name="reader">The pipe to read from.</param>
    /// <param name="destination">The buffer writer used as destination.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static Task CopyToAsync(this PipeReader reader, IBufferWriter<byte> destination, CancellationToken token = default)
        => CopyToAsync(reader, new BufferConsumer<byte>(destination), token);

    /// <summary>
    /// Reads all chunks of data from the pipe.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>A sequence of data chunks.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAllAsync(this PipeReader reader, [EnumeratorCancellation] CancellationToken token = default)
    {
        ReadResult result;
        do
        {
            result = await reader.ReadAsync(token).ConfigureAwait(false);
            result.ThrowIfCancellationRequested(reader, token);
            var buffer = result.Buffer;
            var consumed = buffer.Start;

            try
            {
                for (var position = consumed; buffer.TryGet(ref position, out var block); consumed = position)
                    yield return block;

                consumed = buffer.End;
            }
            finally
            {
                reader.AdvanceTo(consumed);
            }
        }
        while (!result.IsCompleted);
    }

    /// <summary>
    /// Decodes null-terminated UTF-8 encoded string.
    /// </summary>
    /// <remarks>
    /// This method returns when writer side completed or null char reached.
    /// </remarks>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="output">The output buffer for decoded characters.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="reader"/> is <see langword="null"/>;
    /// or <paramref name="output"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask ReadUtf8Async(this PipeReader reader, IBufferWriter<char> output, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(output);

        var decoder = Encoding.UTF8.GetDecoder();
        ReadResult result;

        do
        {
            result = await reader.ReadAsync(token).ConfigureAwait(false);
        }
        while (!Decode(decoder, reader, in result, output, token));

        static bool Decode(Decoder decoder, PipeReader reader, in ReadResult result, IBufferWriter<char> output, CancellationToken token)
        {
            bool completed;
            var buffer = result.Buffer;

            if (buffer.PositionOf(DecodingContext.Utf8NullChar).TryGetValue(out var consumed))
            {
                buffer = buffer.Slice(0, consumed);
                completed = true;
                consumed = result.Buffer.GetPosition(1L, consumed);
            }
            else
            {
                completed = result.IsCompleted;
                consumed = buffer.End;
            }

            decoder.Convert(in buffer, output, completed, out _, out _);
            result.ThrowIfCancellationRequested(reader, token);
            reader.AdvanceTo(consumed);

            return completed;
        }
    }
}
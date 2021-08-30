using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Missing = System.Reflection.Missing;

namespace DotNext.IO.Pipelines
{
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

            void IBufferReader<Missing>.Append(ReadOnlySpan<byte> block, ref int consumedBytes)
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
            for (SequencePosition consumed; parser.RemainingBytes > 0 & !completed; reader.AdvanceTo(consumed))
            {
                var readResult = await reader.ReadAsync(token).ConfigureAwait(false);
                readResult.ThrowIfCancellationRequested(token);
                parser.Append<TResult, TParser>(readResult.Buffer, out consumed);
                completed = readResult.IsCompleted;
            }

            if (parser.RemainingBytes > 0)
                parser.EndOfStream();

            return parser.Complete();
        }

        private static async ValueTask<TResult> ReadAsync<TResult, TDecoder>(this PipeReader reader, TDecoder decoder, LengthFormat lengthFormat, DecodingContext context, CancellationToken token)
            where TResult : struct
            where TDecoder : struct, ISpanDecoder<TResult>
        {
            var length = await ReadLengthAsync(reader, lengthFormat, token).ConfigureAwait(false);
            if (length == 0)
                throw new EndOfStreamException();
            using var buffer = new ArrayBuffer<char>(length);
            var parser = new StringReader<ArrayBuffer<char>>(in context, buffer);
            var completed = false;

            for (SequencePosition consumed; parser.RemainingBytes > 0 & !completed; reader.AdvanceTo(consumed))
            {
                var readResult = await reader.ReadAsync(token).ConfigureAwait(false);
                readResult.ThrowIfCancellationRequested(token);
                parser.Append<string, StringReader<ArrayBuffer<char>>>(readResult.Buffer, out consumed);
                completed = readResult.IsCompleted;
            }

            return parser.RemainingBytes == 0 ? decoder.Decode(parser.Complete()) : throw new EndOfStreamException();
        }

        private static async ValueTask<int> ComputeHashAsync(PipeReader reader, HashAlgorithmName name, int? count, Memory<byte> output, CancellationToken token)
        {
            using var hash = IncrementalHash.CreateHash(name);
            await reader.ReadAsync<Missing, IncrementalHashBuilder>(new IncrementalHashBuilder(hash, count), token).ConfigureAwait(false);
#if NETSTANDARD2_1
            if (!hash.TryGetHashAndReset(output.Span, out var bytesWritten))
#else
            if (!hash.TryGetCurrentHash(output.Span, out var bytesWritten))
#endif
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
        /// <exception cref="EndOfStreamException"><paramref name="reader"/> doesn't contain the necessary number of bytes to restore string.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async ValueTask<string> ReadStringAsync(this PipeReader reader, int length, DecodingContext context, CancellationToken token = default)
        {
            if (length == 0)
                return string.Empty;
            using var resultBuffer = new ArrayBuffer<char>(length);
            return await ReadAsync<string, StringReader<ArrayBuffer<char>>>(reader, new StringReader<ArrayBuffer<char>>(context, resultBuffer), token).ConfigureAwait(false);
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
        /// Reads value of blittable type from pipe.
        /// </summary>
        /// <typeparam name="T">The blittable type to decode.</typeparam>
        /// <param name="reader">The pipe reader.</param>
        /// <param name="token">The token that can be used to cancel operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<T> ReadAsync<T>(this PipeReader reader, CancellationToken token = default)
            where T : unmanaged
            => ReadAsync<T, ValueReader<T>>(reader, new ValueReader<T>(), token);

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
            ReadResult result;
            do
            {
                result = await reader.ReadAsync(token).ConfigureAwait(false);
                result.ThrowIfCancellationRequested(token);
                var buffer = result.Buffer;
                var consumed = buffer.Start;

                try
                {
                    for (var position = consumed; buffer.TryGet(ref position, out var block); consumed = position)
                        await consumer.Invoke(block, token).ConfigureAwait(false);
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
        public static async ValueTask<long> ReadInt64Async(this PipeReader reader, bool littleEndian, CancellationToken token = default)
        {
            var result = await reader.ReadAsync<long>(token).ConfigureAwait(false);
            result.ReverseIfNeeded(littleEndian);
            return result;
        }

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
        public static async ValueTask<ulong> ReadUInt64Async(this PipeReader reader, bool littleEndian, CancellationToken token = default)
        {
            var result = await reader.ReadAsync<ulong>(token).ConfigureAwait(false);
            result.ReverseIfNeeded(littleEndian);
            return result;
        }

        /// <summary>
        /// Decodes 32-bit signed integer using the specified endianness.
        /// </summary>
        /// <param name="reader">The pipe reader.</param>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public static async ValueTask<int> ReadInt32Async(this PipeReader reader, bool littleEndian, CancellationToken token = default)
        {
            var result = await reader.ReadAsync<int>(token).ConfigureAwait(false);
            result.ReverseIfNeeded(littleEndian);
            return result;
        }

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
        public static async ValueTask<uint> ReadUInt32Async(this PipeReader reader, bool littleEndian, CancellationToken token = default)
        {
            var result = await reader.ReadAsync<uint>(token).ConfigureAwait(false);
            result.ReverseIfNeeded(littleEndian);
            return result;
        }

        /// <summary>
        /// Decodes 16-bit signed integer using the specified endianness.
        /// </summary>
        /// <param name="reader">The pipe reader.</param>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public static async ValueTask<short> ReadInt16Async(this PipeReader reader, bool littleEndian, CancellationToken token = default)
        {
            var result = await reader.ReadAsync<short>(token).ConfigureAwait(false);
            result.ReverseIfNeeded(littleEndian);
            return result;
        }

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
        public static async ValueTask<ushort> ReadUInt16Async(this PipeReader reader, bool littleEndian, CancellationToken token = default)
        {
            var result = await reader.ReadAsync<ushort>(token).ConfigureAwait(false);
            result.ReverseIfNeeded(littleEndian);
            return result;
        }

        /// <summary>
        /// Reads the block of memory.
        /// </summary>
        /// <param name="reader">The pipe reader.</param>
        /// <param name="output">The block of memory to fill from the pipe.</param>
        /// <param name="token">The token that can be used to cancel operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="EndOfStreamException">Reader doesn't have enough data.</exception>
        public static ValueTask ReadBlockAsync(this PipeReader reader, Memory<byte> output, CancellationToken token = default)
        {
            if (output.IsEmpty)
                return ValueTask.CompletedTask;

            if (TryReadBlock(reader, output.Length, out var readResult))
            {
                readResult.Buffer.CopyTo(output.Span);
                reader.AdvanceTo(readResult.Buffer.GetPosition(output.Length));

                return readResult.IsCanceled ? ValueTask.FromCanceled(token.IsCancellationRequested ? token : new(true)) : ValueTask.CompletedTask;
            }

            return ReadBlockSlowAsync(reader, output, token);

            async ValueTask ReadBlockSlowAsync(PipeReader reader, Memory<byte> output, CancellationToken token)
                => await ReadAsync<Missing, MemoryReader>(reader, new MemoryReader(output), token).ConfigureAwait(false);
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
        public static async ValueTask<MemoryOwner<byte>> ReadBlockAsync(this PipeReader reader, LengthFormat lengthFormat, MemoryAllocator<byte>? allocator = null, CancellationToken token = default)
        {
            var length = await reader.ReadLengthAsync(lengthFormat, token).ConfigureAwait(false);
            MemoryOwner<byte> result;
            if (length > 0)
            {
                result = allocator.Invoke(length, true);
                await ReadAsync<Missing, MemoryReader>(reader, new MemoryReader(result.Memory), token).ConfigureAwait(false);
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
        public static bool TryReadBlock(this PipeReader reader, long length, out ReadResult result)
        {
            if (reader.TryRead(out result) && length <= result.Buffer.Length)
            {
                result = new(result.Buffer.Slice(0L, length), result.IsCanceled, result.IsCompleted);
                return true;
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

            for (SequencePosition consumed; length > 0L; reader.AdvanceTo(consumed))
            {
                var readResult = await reader.ReadAsync(token).ConfigureAwait(false);
                readResult.ThrowIfCancellationRequested(token);
                var buffer = readResult.Buffer;
                if (buffer.IsEmpty)
                    throw new EndOfStreamException();
                consumed = buffer.Start;
                for (int bytesToConsume; length > 0L && buffer.TryGet(ref consumed, out var block, false) && !block.IsEmpty; consumed = buffer.GetPosition(bytesToConsume, consumed), length -= bytesToConsume)
                {
                    bytesToConsume = Math.Min(block.Length, length.Truncate());
                    await consumer.Invoke(block.Slice(0, bytesToConsume), token).ConfigureAwait(false);
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
#if NETSTANDARD2_1
                < 0L => new(Task.FromException(new ArgumentOutOfRangeException(nameof(length)))),
#else
                < 0L => ValueTask.FromException(new ArgumentOutOfRangeException(nameof(length))),
#endif
                _ => SkipCoreAsync(reader, length, token),
            };

            static async ValueTask SkipCoreAsync(PipeReader reader, long length, CancellationToken token)
            {
                while (length > 0L)
                {
                    var readResult = await reader.ReadAsync(token).ConfigureAwait(false);
                    readResult.ThrowIfCancellationRequested(token);
                    var buffer = readResult.Buffer;
                    if (buffer.IsEmpty)
                        throw new EndOfStreamException();
                    var bytesToConsume = Math.Min(buffer.Length, length);
                    length -= bytesToConsume;
                    reader.AdvanceTo(buffer.GetPosition(bytesToConsume));
                }
            }
        }

        /// <summary>
        /// Decodes 8-bit unsigned integer from its string representation.
        /// </summary>
        /// <param name="reader">The pipe to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<byte> ReadByteAsync(this PipeReader reader, LengthFormat lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<byte, NumberDecoder>(reader, new NumberDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes 8-bit signed integer from its string representation.
        /// </summary>
        /// <param name="reader">The pipe to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [CLSCompliant(false)]
        public static ValueTask<sbyte> ReadSByteAsync(this PipeReader reader, LengthFormat lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<sbyte, NumberDecoder>(reader, new NumberDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes 16-bit signed integer from its string representation.
        /// </summary>
        /// <param name="reader">The pipe to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<short> ReadInt16Async(this PipeReader reader, LengthFormat lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<short, NumberDecoder>(reader, new NumberDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes 16-bit unsigned integer from its string representation.
        /// </summary>
        /// <param name="reader">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [CLSCompliant(false)]
        public static ValueTask<ushort> ReadUInt16Async(this PipeReader reader, LengthFormat lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<ushort, NumberDecoder>(reader, new NumberDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes 32-bit signed integer from its string representation.
        /// </summary>
        /// <param name="reader">The pipe to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<int> ReadInt32Async(this PipeReader reader, LengthFormat lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<int, NumberDecoder>(reader, new NumberDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes 32-bit unsigned integer from its string representation.
        /// </summary>
        /// <param name="reader">The pipe to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [CLSCompliant(false)]
        public static ValueTask<uint> ReadUInt32Async(this PipeReader reader, LengthFormat lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<uint, NumberDecoder>(reader, new NumberDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes 64-bit signed integer from its string representation.
        /// </summary>
        /// <param name="reader">The pipe to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<long> ReadInt64Async(this PipeReader reader, LengthFormat lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<long, NumberDecoder>(reader, new NumberDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes 64-bit unsigned integer from its string representation.
        /// </summary>
        /// <param name="reader">The pipe to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [CLSCompliant(false)]
        public static ValueTask<ulong> ReadUInt64Async(this PipeReader reader, LengthFormat lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<ulong, NumberDecoder>(reader, new NumberDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes single-precision floating-point number from its string representation.
        /// </summary>
        /// <param name="reader">The pipe to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<float> ReadSingleAsync(this PipeReader reader, LengthFormat lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.AllowThousands | NumberStyles.Float, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<float, NumberDecoder>(reader, new NumberDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes double-precision floating-point number from its string representation.
        /// </summary>
        /// <param name="reader">The pipe to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<double> ReadDoubleAsync(this PipeReader reader, LengthFormat lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.AllowThousands | NumberStyles.Float, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<double, NumberDecoder>(reader, new NumberDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes <see cref="decimal"/> from its string representation.
        /// </summary>
        /// <param name="reader">The pipe to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<decimal> ReadDecimalAsync(this PipeReader reader, LengthFormat lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Number, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<decimal, NumberDecoder>(reader, new NumberDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes <see cref="BigInteger"/> from its string representation.
        /// </summary>
        /// <param name="reader">The pipe to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<BigInteger> ReadBigIntegerAsync(this PipeReader reader, LengthFormat lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<BigInteger, NumberDecoder>(reader, new NumberDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes <see cref="Guid"/> from its string representation.
        /// </summary>
        /// <param name="reader">The pipe to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">GUID value is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<Guid> ReadGuidAsync(this PipeReader reader, LengthFormat lengthFormat, DecodingContext context, CancellationToken token = default)
            => ReadAsync<Guid, GuidDecoder>(reader, new GuidDecoder(), lengthFormat, context, token);

        /// <summary>
        /// Decodes <see cref="Guid"/> from its string representation.
        /// </summary>
        /// <param name="reader">The pipe to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="format">The expected format of GUID value.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">GUID value is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<Guid> ReadGuidAsync(this PipeReader reader, LengthFormat lengthFormat, DecodingContext context, string format, CancellationToken token = default)
            => ReadAsync<Guid, GuidDecoder>(reader, new GuidDecoder(format), lengthFormat, context, token);

        /// <summary>
        /// Decodes <see cref="DateTime"/> from its string representation.
        /// </summary>
        /// <param name="reader">The pipe to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The date/time string is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<DateTime> ReadDateTimeAsync(this PipeReader reader, LengthFormat lengthFormat, DecodingContext context, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<DateTime, DateTimeDecoder>(reader, new DateTimeDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes <see cref="DateTime"/> from its string representation.
        /// </summary>
        /// <param name="reader">The pipe to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="formats">An array of allowable formats.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The date/time string is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<DateTime> ReadDateTimeAsync(this PipeReader reader, LengthFormat lengthFormat, DecodingContext context, string[] formats, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<DateTime, DateTimeDecoder>(reader, new DateTimeDecoder(style, formats, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes <see cref="DateTimeOffset"/> from its string representation.
        /// </summary>
        /// <param name="reader">The pipe to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The date/time string is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<DateTimeOffset> ReadDateTimeOffsetAsync(this PipeReader reader, LengthFormat lengthFormat, DecodingContext context, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<DateTimeOffset, DateTimeDecoder>(reader, new DateTimeDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes <see cref="DateTimeOffset"/> from its string representation.
        /// </summary>
        /// <param name="reader">The pipe to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="formats">An array of allowable formats.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The date/time string is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<DateTimeOffset> ReadDateTimeOffsetAsync(this PipeReader reader, LengthFormat lengthFormat, DecodingContext context, string[] formats, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<DateTimeOffset, DateTimeDecoder>(reader, new DateTimeDecoder(style, formats, provider), lengthFormat, context, token);

        /// <summary>
        /// Parses <see cref="TimeSpan"/> from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="reader">The pipe to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The time span is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public static ValueTask<TimeSpan> ReadTimeSpanAsync(this PipeReader reader, LengthFormat lengthFormat, DecodingContext context, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<TimeSpan, TimeSpanDecoder>(reader, new TimeSpanDecoder(provider), lengthFormat, context, token);

        /// <summary>
        /// Parses <see cref="TimeSpan"/> from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="reader">The pipe to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="formats">An array of allowable formats.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The time span is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public static ValueTask<TimeSpan> ReadTimeSpanAsync(this PipeReader reader, LengthFormat lengthFormat, DecodingContext context, string[] formats, TimeSpanStyles style = TimeSpanStyles.None, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<TimeSpan, TimeSpanDecoder>(reader, new TimeSpanDecoder(style, formats, provider), lengthFormat, context, token);

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
    }
}

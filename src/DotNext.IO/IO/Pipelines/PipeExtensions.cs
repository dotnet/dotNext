using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Missing = System.Reflection.Missing;

namespace DotNext.IO.Pipelines
{
    using Buffers;
    using Security.Cryptography;
    using Text;

    /// <summary>
    /// Represents extension method for parsing data stored in pipe.
    /// </summary>
    public static class PipeExtensions
    {
        [StructLayout(LayoutKind.Auto)]
        private struct HashReader : IBufferReader<HashBuilder>
        {
            private readonly HashBuilder builder;
            private int remainingBytes;
            private readonly bool limited;

            internal HashReader(HashAlgorithm algorithm, int? count)
            {
                builder = new HashBuilder(algorithm);
                if (count.HasValue)
                {
                    limited = true;
                    remainingBytes = count.Value;
                }
                else
                {
                    limited = false;
                    remainingBytes = 4096;
                }
            }

            readonly int IBufferReader<HashBuilder>.RemainingBytes => remainingBytes;

            readonly HashBuilder IBufferReader<HashBuilder>.Complete() => builder;

            void IBufferReader<HashBuilder>.EndOfStream()
                => remainingBytes = limited ? throw new EndOfStreamException() : 0;

            void IBufferReader<HashBuilder>.Append(ReadOnlySpan<byte> block, ref int consumedBytes)
            {
                builder.Add(block);
                if (limited)
                    remainingBytes -= block.Length;
            }
        }

        [StructLayout(LayoutKind.Auto)]
        private struct LengthWriter : SevenBitEncodedInt.IWriter
        {
            private readonly Memory<byte> writer;
            private int offset;

            internal LengthWriter(IBufferWriter<byte> output)
            {
                writer = output.GetMemory(5);
                offset = 0;
            }
            internal readonly int Count => offset;
            void SevenBitEncodedInt.IWriter.WriteByte(byte value)
            {
                writer.Span[offset++] = value;
            }
        }

        private static async ValueTask<TResult> ReadAsync<TResult, TParser>(this PipeReader reader, TParser parser, CancellationToken token)
            where TParser : struct, IBufferReader<TResult>
        {
            for (SequencePosition consumed; parser.RemainingBytes > 0; reader.AdvanceTo(consumed))
            {
                var readResult = await reader.ReadAsync(token).ConfigureAwait(false);
                readResult.ThrowIfCancellationRequested(token);
                parser.Append<TResult, TParser>(readResult.Buffer, out consumed);
            }
            return parser.Complete();
        }

        internal static async ValueTask ComputeHashAsync(this PipeReader reader, HashAlgorithm algorithm, int? count, Memory<byte> output, CancellationToken token)
        {
            using var builder = await reader.ReadAsync<HashBuilder, HashReader>(new HashReader(algorithm, count), token).ConfigureAwait(false);
            builder.Build(output.Span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ValueTask<int> Read7BitEncodedIntAsync(this PipeReader reader, CancellationToken token)
            => reader.ReadAsync<int, SevenBitEncodedIntReader>(new SevenBitEncodedIntReader(5), token);

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
            return await ReadAsync<string, StringReader<ArrayBuffer<char>>>(reader, new StringReader<ArrayBuffer<char>>(context, resultBuffer), token);
        }

        private static async ValueTask<int> ReadLengthAsync(this PipeReader reader, StringLengthEncoding lengthFormat, CancellationToken token)
        {
            ValueTask<int> result;
            var littleEndian = BitConverter.IsLittleEndian;
            switch (lengthFormat)
            {
                default:
                    throw new ArgumentOutOfRangeException(nameof(lengthFormat));
                case StringLengthEncoding.Plain:
                    result = reader.ReadAsync<int>(token);
                    break;
                case StringLengthEncoding.PlainLittleEndian:
                    littleEndian = true;
                    goto case StringLengthEncoding.Plain;
                case StringLengthEncoding.PlainBigEndian:
                    littleEndian = false;
                    goto case StringLengthEncoding.Plain;
                case StringLengthEncoding.Compressed:
                    result = reader.Read7BitEncodedIntAsync(token);
                    break;
            }
            var length = await result.ConfigureAwait(false);
            length.ReverseIfNeeded(littleEndian);
            return length;
        }

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
        public static async ValueTask<string> ReadStringAsync(this PipeReader reader, StringLengthEncoding lengthFormat, DecodingContext context, CancellationToken token = default)
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
        /// Reads the block of memory.
        /// </summary>
        /// <param name="reader">The pipe reader.</param>
        /// <param name="output">The block of memory to fill from the pipe.</param>
        /// <param name="token">The token that can be used to cancel operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="EndOfStreamException">Reader doesn't have enough data.</exception>
        public static async ValueTask ReadAsync(this PipeReader reader, Memory<byte> output, CancellationToken token = default)
            => await ReadAsync<Missing, MemoryReader>(reader, new MemoryReader(output), token).ConfigureAwait(false);
        
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
        /// Encodes value of blittable type.
        /// </summary>
        /// <typeparam name="T">The blittable type to encode.</typeparam>
        /// <param name="writer">The pipe writer.</param>
        /// <param name="value">The value to be encoded in binary form.</param>
        /// <param name="token">The token that can be used to cancel operation.</param>
        /// <returns>The task representing asynchronous result of operation.</returns>
        public static ValueTask<FlushResult> WriteAsync<T>(this PipeWriter writer, T value, CancellationToken token = default)
            where T : unmanaged
        {
            writer.Write(Span.AsReadOnlyBytes(in value));
            return writer.FlushAsync(token);
        }

        private static void Write7BitEncodedInt(this IBufferWriter<byte> output, int value)
        {
            var writer = new LengthWriter(output);
            SevenBitEncodedInt.Encode(ref writer, (uint)value);
            output.Advance(writer.Count);
        }

        private static ValueTask<FlushResult> WriteLengthAsync(this PipeWriter writer, ReadOnlyMemory<char> value, Encoding encoding, StringLengthEncoding? lengthFormat, CancellationToken token)
        {
            ValueTask<FlushResult> result;
            if (lengthFormat is null)
                result = new ValueTask<FlushResult>(new FlushResult(false, false));
            else
            {
                var length = encoding.GetByteCount(value.Span);
                switch (lengthFormat.Value)
                {
                    default:
                        throw new ArgumentOutOfRangeException(nameof(lengthFormat));
                    case StringLengthEncoding.PlainLittleEndian:
                        length.ReverseIfNeeded(true);
                        goto case StringLengthEncoding.Plain;
                    case StringLengthEncoding.PlainBigEndian:
                        length.ReverseIfNeeded(false);
                        goto case StringLengthEncoding.Plain;
                    case StringLengthEncoding.Plain:
                        result = writer.WriteAsync(length, token);
                        break;
                    case StringLengthEncoding.Compressed:
                        writer.Write7BitEncodedInt(length);
                        result = writer.FlushAsync(token);
                        break;
                }
            }
            return result;
        }

        /// <summary>
        /// Encodes the string to bytes and write them to pipe asynchronously.
        /// </summary>
        /// <param name="writer">The pipe writer.</param>
        /// <param name="value">The block of characters to encode.</param>
        /// <param name="context">The text encoding context.</param>
        /// <param name="bufferSize">The buffer size (in bytes) used for encoding.</param>
        /// <param name="lengthFormat">String length encoding format; or <see langword="null"/> to prevent encoding of string length.</param>
        /// <param name="token">The token that can be used to cancel operation.</param>
        /// <returns>The result of operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        /// <exception cref="EndOfStreamException">Pipe closed unexpectedly.</exception>
        public static async ValueTask WriteStringAsync(this PipeWriter writer, ReadOnlyMemory<char> value, EncodingContext context, int bufferSize = 0, StringLengthEncoding? lengthFormat = null, CancellationToken token = default)
        {
            var result = await writer.WriteLengthAsync(value, context.Encoding, lengthFormat, token).ConfigureAwait(false);
            result.ThrowIfCancellationRequested(token);
            if(value.Length == 0)
                return;
            var encoder = context.GetEncoder();
            for (int charsLeft = value.Length, charsUsed, maxChars, bytesPerChar = context.Encoding.GetMaxByteCount(1); charsLeft > 0; value = value.Slice(charsUsed), charsLeft -= charsUsed)
            {
                if(result.IsCompleted)
                    throw new EndOfStreamException();
                var buffer = writer.GetMemory(bufferSize);
                maxChars = buffer.Length / bytesPerChar;
                charsUsed = Math.Min(maxChars, charsLeft);
                encoder.Convert(value.Span.Slice(0, charsUsed), buffer.Span, charsUsed == charsLeft, out charsUsed, out var bytesUsed, out _);
                writer.Advance(bytesUsed);
                result = await writer.FlushAsync(token).ConfigureAwait(false);
                result.ThrowIfCancellationRequested(token);
            }
        }
    }
}

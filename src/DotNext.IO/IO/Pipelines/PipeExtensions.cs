using System;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO.Pipelines
{
    using Buffers;
    using System.Buffers;
    using Text;
    using Intrinsics = Runtime.Intrinsics;

    /// <summary>
    /// Represents extension method for parsing data stored in pipe.
    /// </summary>
    public static class PipeExtensions
    {
        private interface IBufferReader<out T>
        {
            int RemainingBytes { get; }

            void Append(ReadOnlySpan<byte> block, ref int consumedBytes);

            T Complete();
        }

        [StructLayout(LayoutKind.Auto)]
        private struct StringReader : IBufferReader<string>
        {
            private readonly Decoder decoder;
            private readonly Encoding encoding;
            private int length, resultOffset;
            private readonly Memory<char> result;

            internal StringReader(in DecodingContext context, Memory<char> result)
            {
                decoder = context.GetDecoder();
                encoding = context.Encoding;
                length = result.Length;
                this.result = result;
                resultOffset = 0;
            }

            readonly int IBufferReader<string>.RemainingBytes => length;

            readonly string IBufferReader<string>.Complete() => new string(result.Span.Slice(0, resultOffset));

            void IBufferReader<string>.Append(ReadOnlySpan<byte> bytes, ref int consumedBytes)
            {
                length -= bytes.Length;
                resultOffset += decoder.GetChars(bytes, result.Span.Slice(resultOffset), length == 0);
            }
        }

        [StructLayout(LayoutKind.Auto)]
        private struct SevenBitEncodedIntReader : IBufferReader<int>
        {
            private int remainingBytes;
            private SevenBitEncodedInt.Reader reader;

            internal SevenBitEncodedIntReader(int remainingBytes)
            {
                this.remainingBytes = remainingBytes;
                reader = new SevenBitEncodedInt.Reader();
            }

            readonly int IBufferReader<int>.RemainingBytes => remainingBytes;

            void IBufferReader<int>.Append(ReadOnlySpan<byte> block, ref int consumedBytes)
            {
                consumedBytes = 0;
                foreach (var b in block)
                {
                    consumedBytes += 1;
                    if (reader.Append(b))
                        remainingBytes -= 1;
                    else
                    {
                        remainingBytes = 0;
                        break;
                    }
                }
            }

            readonly int IBufferReader<int>.Complete() => (int)reader.Result;
        }

        [StructLayout(LayoutKind.Auto)]
        private struct ValueReader<T> : IBufferReader<T>
            where T : unmanaged
        {
            private T result;
            private int offset;

            unsafe readonly int IBufferReader<T>.RemainingBytes => sizeof(T) - offset;

            readonly T IBufferReader<T>.Complete() => result;

            void IBufferReader<T>.Append(ReadOnlySpan<byte> block, ref int consumedBytes)
            {
                block.CopyTo(Intrinsics.AsSpan(ref result).Slice(offset));
                offset += block.Length;

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

        private static void Append<TResult, TParser>(this ref TParser parser, in ReadOnlySequence<byte> input, out SequencePosition consumed)
            where TParser : struct, IBufferReader<TResult>
        {
            if (input.IsEmpty)
                throw new EndOfStreamException();
            int bytesToConsume;
            for (consumed = input.Start; parser.RemainingBytes > 0 && input.TryGet(ref consumed, out var block, false) && block.Length > 0; consumed = input.GetPosition(bytesToConsume, consumed))
            {
                bytesToConsume = Math.Min(block.Length, parser.RemainingBytes);
                block = block.Slice(0, bytesToConsume);
                parser.Append(block.Span, ref bytesToConsume);
            }
        }

        private static async ValueTask<TResult> ReadAsync<TResult, TParser>(this PipeReader reader, TParser parser, CancellationToken token = default)
            where TParser : struct, IBufferReader<TResult>
        {
            for (SequencePosition consumed; parser.RemainingBytes > 0; reader.AdvanceTo(consumed))
            {
                var readResult = await reader.ReadAsync(token).ConfigureAwait(false);
                if (readResult.IsCanceled)
                    throw new OperationCanceledException(token.IsCancellationRequested ? token : new CancellationToken(true));
                parser.Append<TResult, TParser>(readResult.Buffer, out consumed);
            }
            return parser.Complete();
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
            using var resultBuffer = new ArrayRental<char>(length);
            return await ReadAsync<string, StringReader>(reader, new StringReader(context, resultBuffer.Memory), token);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ValueTask<int> ReadStringLengthAsync(this PipeReader reader, StringLengthEncoding lengthFormat, CancellationToken token) => lengthFormat switch
        {
            StringLengthEncoding.Plain => reader.ReadAsync<int>(token),
            StringLengthEncoding.Compressed => reader.Read7BitEncodedIntAsync(token),
            _ => throw new ArgumentOutOfRangeException(nameof(lengthFormat))
        };

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
            => await ReadStringAsync(reader, await reader.ReadStringLengthAsync(lengthFormat, token).ConfigureAwait(false), context, token).ConfigureAwait(false);

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
        /// Encodes value of blittable type.
        /// </summary>
        /// <typeparam name="T">The blittable type to encode.</typeparam>
        /// <param name="writer">The pipe writer.</param>
        /// <param name="value">The value to be encoded in binary form.</param>
        /// <param name="token">The token that can be used to cancel operation.</param>
        /// <returns>The task representing asynchronous result of operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async ValueTask WriteAsync<T>(this PipeWriter writer, T value, CancellationToken token = default)
            where T : unmanaged
        {
            writer.Write(Intrinsics.AsReadOnlySpan(in value));
            var result = await writer.FlushAsync(token).ConfigureAwait(false);
            if (result.IsCanceled)
                throw new OperationCanceledException(token.IsCancellationRequested ? token : new CancellationToken(false));
        }

        private static void Write7BitEncodedInt(this IBufferWriter<byte> output, int value)
        {
            var writer = new LengthWriter(output);
            SevenBitEncodedInt.Encode(ref writer, (uint)value);
            output.Advance(writer.Count);
        }

        /// <summary>
        /// Encodes the string to bytes and write them to pipe asynchronously.
        /// </summary>
        /// <param name="writer">The pipe writer.</param>
        /// <param name="value">The block of characters to encode.</param>
        /// <param name="context">The text encoding context.</param>
        /// <param name="bufferSize">The buffer size (in bytes) used for encoding.</param>
        /// <param name="lengthFormat">Represents string length encoding format.</param>
        /// <param name="token">The token that can be used to cancel operation.</param>
        /// <returns>The result of operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static async ValueTask WriteStringAsync(this PipeWriter writer, ReadOnlyMemory<char> value, EncodingContext context, int bufferSize = 0, StringLengthEncoding? lengthFormat = null, CancellationToken token = default)
        {
            FlushResult result;
            switch (lengthFormat)
            {
                default:
                    throw new ArgumentOutOfRangeException(nameof(lengthFormat));
                case null:
                    break;
                case StringLengthEncoding.Plain:
                    await writer.WriteAsync(context.Encoding.GetByteCount(value.Span), token).ConfigureAwait(false);
                    break;
                case StringLengthEncoding.Compressed:
                    writer.Write7BitEncodedInt(context.Encoding.GetByteCount(value.Span));
                    result = await writer.FlushAsync(token).ConfigureAwait(false);
                    if (result.IsCompleted)
                        return;
                    if (result.IsCanceled)
                        throw new OperationCanceledException(token.IsCancellationRequested ? token : new CancellationToken(false));
                    break;
            }
            if (value.Length == 0)
                return;
            var encoder = context.GetEncoder();
            for (int charsLeft = value.Length, charsUsed, maxChars, bytesPerChar = context.Encoding.GetMaxByteCount(1); charsLeft > 0; value = value.Slice(charsUsed), charsLeft -= charsUsed)
            {
                var buffer = writer.GetMemory(bufferSize);
                maxChars = buffer.Length / bytesPerChar;
                charsUsed = Math.Min(maxChars, charsLeft);
                encoder.Convert(value.Span.Slice(0, charsUsed), buffer.Span, charsUsed == charsLeft, out charsUsed, out var bytesUsed, out _);
                writer.Advance(bytesUsed);
                result = await writer.FlushAsync(token).ConfigureAwait(false);
                if (result.IsCompleted)
                    break;
                if (result.IsCanceled)
                    throw new OperationCanceledException(token.IsCancellationRequested ? token : new CancellationToken(false));
            }
        }
    }
}

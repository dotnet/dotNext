using System;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO.Pipelines
{
    using Intrinsics = Runtime.Intrinsics;
    using Buffers;
    using Text;
    using System.Buffers;

    /// <summary>
    /// Represents extension method for parsing data stored in pipe.
    /// </summary>
    public static class PipeExtensions
    {
        private interface IBufferReader<out T>
        {
            int RemainingBytes { get; }

            void Append(ReadOnlySpan<byte> block);
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

            int IBufferReader<string>.RemainingBytes => length;

            string IBufferReader<string>.Complete() => new string(result.Span.Slice(0, resultOffset));

            private static void GetChars(Decoder decoder, ReadOnlySpan<byte> bytes, int charCount, Span<char> output, ref int outputOffset, ref int length)
            {
                using MemoryRental<char> charBuffer = charCount <= 1024 ? stackalloc char[charCount] : new MemoryRental<char>(charCount);
                length -= bytes.Length;
                charCount = decoder.GetChars(bytes, charBuffer.Span, length == 0);
                Intrinsics.Copy(ref charBuffer[0], ref output[outputOffset], charCount);
                outputOffset += charCount;
            }

            void IBufferReader<string>.Append(ReadOnlySpan<byte> bytes) => GetChars(decoder, bytes, encoding.GetMaxCharCount(bytes.Length), result.Span, ref resultOffset, ref length);
        }

        [StructLayout(LayoutKind.Auto)]
        private struct ValueReader<T> : IBufferReader<T>
            where T : unmanaged
        {
            private T result;
            private int offset;

            unsafe int IBufferReader<T>.RemainingBytes => sizeof(T) - offset;

            T IBufferReader<T>.Complete() => result;

            void IBufferReader<T>.Append(ReadOnlySpan<byte> block)
            {
                block.CopyTo(Intrinsics.AsSpan(ref result).Slice(offset));
                offset += block.Length;
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
                parser.Append(block.Span);
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
                return "";
            using var resultBuffer = new ArrayRental<char>(context.Encoding.GetMaxCharCount(length));
            return await ReadAsync<string, StringReader>(reader, new StringReader(context, resultBuffer.Memory), token);
        }

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
            int bytesUsed;
            unsafe
            {
                bytesUsed = sizeof(T);
            }
            Intrinsics.AsReadOnlySpan(in value).CopyTo(writer.GetMemory(bytesUsed).Span);
            writer.Advance(bytesUsed);
            var result = await writer.FlushAsync(token).ConfigureAwait(false);
            if (result.IsCanceled)
                throw new OperationCanceledException(token.IsCancellationRequested ? token : new CancellationToken(false));
        }

        /// <summary>
        /// Encodes the string to bytes and write them to pipe asynchronously.
        /// </summary>
        /// <param name="writer">The pipe writer.</param>
        /// <param name="value">The block of characters to encode.</param>
        /// <param name="context">The text encoding context.</param>
        /// <param name="bufferSize">The buffer size (in bytes) used for encoding.</param>
        /// <param name="token">The token that can be used to cancel operation.</param>
        /// <returns>The result of operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async ValueTask WriteStringAsync(this PipeWriter writer, ReadOnlyMemory<char> value, EncodingContext context, int bufferSize = 0, CancellationToken token = default)
        {
            if (value.Length == 0)
                return;
            var encoder = context.GetEncoder();
            var completed = false;
            for (int offset = 0, charsUsed; !completed; offset += charsUsed)
            {
                var buffer = writer.GetMemory(bufferSize);
                var chars = value.Slice(offset);
                encoder.Convert(chars.Span, buffer.Span, chars.Length == 0, out charsUsed, out var bytesUsed, out completed);
                writer.Advance(bytesUsed);
                value = chars;
                var result = await writer.FlushAsync(token).ConfigureAwait(false);
                if (result.IsCompleted)
                    break;
                if (result.IsCanceled)
                    throw new OperationCanceledException(token.IsCancellationRequested ? token : new CancellationToken(false));
            }
        }
    }
}

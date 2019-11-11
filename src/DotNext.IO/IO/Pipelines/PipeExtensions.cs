using System;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO.Pipelines
{
    using Intrinsics = Runtime.Intrinsics;
    using Buffers;
    using Text;

    public static class PipeExtensions
    {
        private static void GetChars(this Decoder decoder, ReadOnlySpan<byte> bytes, int charCount, Span<char> output, ref int outputOffset, ref int length)
        {
            using MemoryRental<char> charBuffer = charCount <= 1024 ? stackalloc char[charCount] : new MemoryRental<char>(charCount);
            length -= bytes.Length;
            charCount = decoder.GetChars(bytes, charBuffer.Span, length == 0);
            Intrinsics.Copy(ref charBuffer[0], ref output[outputOffset], charCount);
            outputOffset += charCount;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="length"></param>
        /// <param name="context"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        /// <exception cref="EndOfStreamException"><paramref name="reader"/> doesn't contain the necessary number of bytes to restore string.</exception>
        public static async ValueTask<string> ReadStringAsync(this PipeReader reader, int length, DecodingContext context, CancellationToken token = default)
        {
            if (length == 0)
                return "";
            using var result = new ArrayRental<char>(context.Encoding.GetMaxCharCount(length));
            var resultOffset = 0; //offset in result buffer
            var decoder = context.GetDecoder();
            for (SequencePosition consumed = default; length > 0; reader.AdvanceTo(consumed), consumed = default)
            {
                var readResult = await reader.ReadAsync(token).ConfigureAwait(false);
                if (readResult.IsCanceled)
                    throw new OperationCanceledException(token.IsCancellationRequested ? token : new CancellationToken(true));
                if (readResult.IsCompleted)
                    throw new EndOfStreamException();
                var buffer = readResult.Buffer;
                int bytesToConsume;
                for (var position = buffer.Start; length > 0 && buffer.TryGet(ref position, out var block); consumed = buffer.GetPosition(bytesToConsume, position))
                {
                    bytesToConsume = Math.Min(length, block.Length);
                    decoder.GetChars(block.Slice(0, bytesToConsume).Span, context.Encoding.GetMaxCharCount(bytesToConsume), result.Span, ref resultOffset, ref length);
                }
            }
            return new string(result.Span.Slice(0, resultOffset));
        }

        public static async ValueTask<T> ReadAsync<T>(this PipeReader reader, CancellationToken token = default)
            where T : unmanaged
        {
            var result = new T();
            var offset = 0;
            for (SequencePosition consumed = default; offset < Unsafe.SizeOf<T>(); reader.AdvanceTo(consumed), consumed = default)
            {
                var readResult = await reader.ReadAsync(token).ConfigureAwait(false);
                if (readResult.IsCanceled)
                    throw new OperationCanceledException(token.IsCancellationRequested ? token : new CancellationToken(true));
                if (readResult.IsCompleted)
                    throw new EndOfStreamException();
                var buffer = readResult.Buffer;
                int bytesToConsume;
                for (var position = buffer.Start; offset < Unsafe.SizeOf<T>() && buffer.TryGet(ref position, out var block); consumed = buffer.GetPosition(bytesToConsume, position))
                {
                    bytesToConsume = Math.Min(Unsafe.SizeOf<T>() - offset, block.Length);
                    block.Span.Slice(0, bytesToConsume).CopyTo(Intrinsics.AsSpan(ref result).Slice(offset));
                    offset += bytesToConsume;
                }
            }
            return result;
        }

        public static async ValueTask WriteAsync<T>(this PipeWriter writer, T value, CancellationToken token = default)
            where T : unmanaged
        {
            var bytesUsed = Unsafe.SizeOf<T>();
            Intrinsics.AsSpan(ref value).CopyTo(writer.GetMemory(bytesUsed).Span);
            writer.Advance(bytesUsed);
            var result = await writer.FlushAsync(token).ConfigureAwait(false);
            if (result.IsCanceled)
                throw new OperationCanceledException(token.IsCancellationRequested ? token : new CancellationToken(false));
        }

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

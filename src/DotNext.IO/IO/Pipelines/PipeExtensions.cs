using System;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO.Pipelines
{
    using Memory = Runtime.InteropServices.Memory;
    using Buffers;
    using Text;

    public static class PipeExtensions
    {
        private static void GetChars(this Decoder decoder, ReadOnlySpan<byte> bytes, int charCount, Span<char> output, ref int outputOffset, ref int length)
        {
            using MemoryRental<char> charBuffer = charCount <= 1024 ? stackalloc char[charCount] : new MemoryRental<char>(charCount);
            length -= bytes.Length;
            charCount = decoder.GetChars(bytes, charBuffer.Span, length == 0);
            Memory.Copy(ref charBuffer[0], ref output[outputOffset], (uint)charCount);
            outputOffset += charCount;
        }
         
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

using System;
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace DotNext.Text
{
    using Buffers;

    public partial struct Base64Decoder
    {
        private Span<byte> ReservedBytes => Span.AsBytes(ref reservedBuffer);

        private void DecodeCore(ReadOnlySpan<byte> utf8Chars, IBufferWriter<byte> output)
        {
            var produced = Base64.GetMaxDecodedFromUtf8Length(utf8Chars.Length);
            var buffer = output.GetSpan(produced);

            // x & 3 is the same as x % 4
            switch (Base64.DecodeFromUtf8(utf8Chars, buffer, out var consumed, out produced, (utf8Chars.Length & 3) == 0))
            {
                default:
                    throw new FormatException(ExceptionMessages.MalformedBase64);
                case OperationStatus.DestinationTooSmall:
                case OperationStatus.Done:
                    reservedBufferSize = 0;
                    break;
                case OperationStatus.NeedMoreData:
                    reservedBufferSize = utf8Chars.Length - consumed;
                    Debug.Assert(reservedBufferSize <= 4);
                    utf8Chars.Slice(consumed).CopyTo(ReservedBytes);
                    break;
            }

            output.Advance(produced);
        }

#if !NETSTANDARD2_1
        [SkipLocalsInit]
#endif
        private void CopyAndDecode(ReadOnlySpan<byte> utf8Chars, IBufferWriter<byte> output)
        {
            var newSize = reservedBufferSize + utf8Chars.Length;
            using var tempBuffer = newSize <= MemoryRental<byte>.StackallocThreshold ? stackalloc byte[newSize] : new MemoryRental<byte>(newSize);
            ReservedBytes.Slice(0, reservedBufferSize).CopyTo(tempBuffer.Span);
            utf8Chars.CopyTo(tempBuffer.Span.Slice(reservedBufferSize));
            DecodeCore(tempBuffer.Span, output);
        }

        /// <summary>
        /// Decodes UTF-8 encoded base64 string.
        /// </summary>
        /// <param name="utf8Chars">UTF-8 encoded portion of base64 string.</param>
        /// <param name="output">The output growable buffer used to write decoded bytes.</param>
        /// <exception cref="FormatException">The input base64 string is malformed.</exception>
        public void Decode(ReadOnlySpan<byte> utf8Chars, IBufferWriter<byte> output)
        {
            if (reservedBufferSize > 0)
                CopyAndDecode(utf8Chars, output);
            else
                DecodeCore(utf8Chars, output);
        }

        /// <summary>
        /// Decodes UTF-8 encoded base64 string.
        /// </summary>
        /// <param name="utf8Chars">UTF-8 encoded portion of base64 string.</param>
        /// <param name="output">The output growable buffer used to write decoded bytes.</param>
        /// <exception cref="FormatException">The input base64 string is malformed.</exception>
        public void Decode(in ReadOnlySequence<byte> utf8Chars, IBufferWriter<byte> output)
        {
            foreach (var chunk in utf8Chars)
                Decode(chunk.Span, output);
        }

#if !NETSTANDARD2_1
        [SkipLocalsInit]
#endif
        private void DecodeCore<TArg>(ReadOnlySpan<byte> utf8Chars, in ValueReadOnlySpanAction<byte, TArg> output, TArg arg)
        {
            Span<byte> buffer = stackalloc byte[DecodingBufferSize];

            consume_next_chunk:

            // x & 3 is the same as x % 4
            switch (Base64.DecodeFromUtf8(utf8Chars, buffer, out var consumed, out var produced, (utf8Chars.Length & 3) == 0))
            {
                default:
                    throw new FormatException(ExceptionMessages.MalformedBase64);
                case OperationStatus.Done:
                case OperationStatus.DestinationTooSmall:
                    reservedBufferSize = 0;
                    break;
                case OperationStatus.NeedMoreData:
                    reservedBufferSize = utf8Chars.Length - consumed;
                    Debug.Assert(reservedBufferSize <= 4);
                    utf8Chars.Slice(consumed).CopyTo(ReservedBytes);
                    break;
            }

            if (produced > 0 && consumed > 0)
            {
                output.Invoke(buffer.Slice(0, produced), arg);
                utf8Chars = utf8Chars.Slice(consumed);
                goto consume_next_chunk;
            }
        }

#if !NETSTANDARD2_1
        [SkipLocalsInit]
#endif
        private void CopyAndDecode<TArg>(ReadOnlySpan<byte> utf8Chars, in ValueReadOnlySpanAction<byte, TArg> output, TArg arg)
        {
            var newSize = reservedBufferSize + utf8Chars.Length;
            using var tempBuffer = newSize <= MemoryRental<byte>.StackallocThreshold ? stackalloc byte[newSize] : new MemoryRental<byte>(newSize);
            ReservedBytes.Slice(0, reservedBufferSize).CopyTo(tempBuffer.Span);
            utf8Chars.CopyTo(tempBuffer.Span.Slice(reservedBufferSize));
            DecodeCore(tempBuffer.Span, in output, arg);
        }

        /// <summary>
        /// Decodes UTF-8 encoded base64 string.
        /// </summary>
        /// <typeparam name="TArg">The type of the argument to be passed to the callback.</typeparam>
        /// <param name="utf8Chars">UTF-8 encoded portion of base64 string.</param>
        /// <param name="output">The callback called for decoded portion of data.</param>
        /// <param name="arg">The argument to be passed to the callback.</param>
        /// <exception cref="FormatException">The input base64 string is malformed.</exception>
        public void Decode<TArg>(ReadOnlySpan<byte> utf8Chars, in ValueReadOnlySpanAction<byte, TArg> output, TArg arg)
        {
            if (reservedBufferSize > 0)
                CopyAndDecode(utf8Chars, in output, arg);
            else
                DecodeCore(utf8Chars, in output, arg);
        }

        /// <summary>
        /// Decodes UTF-8 encoded base64 string.
        /// </summary>
        /// <typeparam name="TArg">The type of the argument to be passed to the callback.</typeparam>
        /// <param name="utf8Chars">UTF-8 encoded portion of base64 string.</param>
        /// <param name="output">The callback called for decoded portion of data.</param>
        /// <param name="arg">The argument to be passed to the callback.</param>
        /// <exception cref="FormatException">The input base64 string is malformed.</exception>
        public void Decode<TArg>(in ReadOnlySequence<byte> utf8Chars, in ValueReadOnlySpanAction<byte, TArg> output, TArg arg)
        {
            foreach (var chunk in utf8Chars)
                Decode(chunk.Span, in output, arg);
        }

        /// <summary>
        /// Decodes UTF-8 encoded base64 string and writes result to the stream synchronously.
        /// </summary>
        /// <param name="utf8Chars">UTF-8 encoded portion of base64 string.</param>
        /// <param name="output">The stream used as destination for decoded bytes.</param>
        /// <exception cref="FormatException">The input base64 string is malformed.</exception>
        public unsafe void Decode(ReadOnlySpan<byte> utf8Chars, Stream output)
            => Decode(utf8Chars, new ValueReadOnlySpanAction<byte, Stream>(&Span.CopyTo), output);

        /// <summary>
        /// Decodes UTF-8 encoded base64 string and writes result to the stream synchronously.
        /// </summary>
        /// <param name="utf8Chars">UTF-8 encoded portion of base64 string.</param>
        /// <param name="output">The stream used as destination for decoded bytes.</param>
        /// <exception cref="FormatException">The input base64 string is malformed.</exception>
        public unsafe void Decode(in ReadOnlySequence<byte> utf8Chars, Stream output)
        {
            var callback = new ValueReadOnlySpanAction<byte, Stream>(&Span.CopyTo);
            foreach (var chunk in utf8Chars)
                Decode(chunk.Span, in callback, output);
        }
    }
}
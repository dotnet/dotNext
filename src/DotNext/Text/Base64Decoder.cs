using System;
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Text
{
    using Buffers;

    /// <summary>
    /// Represents base64 decoder suitable for streaming.
    /// </summary>
    /// <remarks>
    /// This type maintains internal state for correct decoding of streaming data.
    /// Therefore, it must be passed by reference to any routine. It's not a <c>ref struct</c>
    /// to allow construction of high-level decoders in the form of classes.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    public struct Base64Decoder
    {
        private uint reservedBuffer; // 4 bytes buffer for decoding base64
        private int reservedBufferSize;

        /// <summary>
        /// Indicates that decoders expected additional data to decode.
        /// </summary>
        public bool NeedMoreData => reservedBufferSize > 0;

        private void DecodeCore(ReadOnlySpan<byte> utf8Chars, IBufferWriter<byte> output)
        {
            var produced = Base64.GetMaxDecodedFromUtf8Length(utf8Chars.Length);
            var buffer = output.GetSpan(produced);

            // x & 3 is the same as x % 4
            switch (Base64.DecodeFromUtf8(utf8Chars, buffer, out var consumed, out produced, (utf8Chars.Length & 3) == 0))
            {
                case OperationStatus.Done:
                    reservedBufferSize = 0;
                    break;
                case OperationStatus.InvalidData:
                    throw new FormatException(ExceptionMessages.MalformedBase64);
                default:
                    reservedBufferSize = utf8Chars.Length - consumed;
                    Debug.Assert(reservedBufferSize <= 4);
                    utf8Chars.Slice(consumed).CopyTo(Span.AsBytes(ref reservedBuffer));
                    break;
            }

            output.Advance(produced);
        }

# if !NETSTANDARD2_1
        [SkipLocalsInit]
#endif
        private void CopyAndDecode(ReadOnlySpan<byte> utf8Chars, IBufferWriter<byte> output)
        {
            var newSize = reservedBufferSize + utf8Chars.Length;
            using var tempBuffer = newSize <= MemoryRental<byte>.StackallocThreshold ? stackalloc byte[newSize] : new MemoryRental<byte>(newSize);
            Span.AsReadOnlyBytes(in reservedBuffer).Slice(0, reservedBufferSize).CopyTo(tempBuffer.Span);
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

# if !NETSTANDARD2_1
        [SkipLocalsInit]
#endif
        private void DecodeCore<TArg>(ReadOnlySpan<byte> utf8Chars, in ValueReadOnlySpanAction<byte, TArg> output, TArg arg)
        {
            Span<byte> buffer = stackalloc byte[256];

            consume_next_chunk:

            // x & 3 is the same as x % 4
            switch (Base64.DecodeFromUtf8(utf8Chars, buffer, out var consumed, out var produced, (utf8Chars.Length & 3) == 0))
            {
                case OperationStatus.Done:
                    reservedBufferSize = 0;
                    break;
                case OperationStatus.InvalidData:
                    throw new FormatException(ExceptionMessages.MalformedBase64);
                default:
                    reservedBufferSize = utf8Chars.Length - consumed;
                    Debug.Assert(reservedBufferSize <= 4);
                    utf8Chars.Slice(consumed).CopyTo(Span.AsBytes(ref reservedBuffer));
                    break;
            }

            if (produced > 0 && consumed > 0)
            {
                output.Invoke(buffer.Slice(0, produced), arg);
                utf8Chars = utf8Chars.Slice(consumed);
                goto consume_next_chunk;
            }
        }

# if !NETSTANDARD2_1
        [SkipLocalsInit]
#endif
        private void CopyAndDecode<TArg>(ReadOnlySpan<byte> utf8Chars, in ValueReadOnlySpanAction<byte, TArg> output, TArg arg)
        {
            var newSize = reservedBufferSize + utf8Chars.Length;
            using var tempBuffer = newSize <= MemoryRental<byte>.StackallocThreshold ? stackalloc byte[newSize] : new MemoryRental<byte>(newSize);
            Span.AsReadOnlyBytes(in reservedBuffer).Slice(0, reservedBufferSize).CopyTo(tempBuffer.Span);
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
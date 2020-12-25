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
        private const int DecodingBufferSize = 256;
        private ulong reservedBuffer; // 8 bytes buffer for decoding base64
        private int reservedBufferSize;

        private Span<byte> ReservedBytes => Span.AsBytes(ref reservedBuffer);

        private Span<char> ReservedChars => MemoryMarshal.CreateSpan(ref Unsafe.As<ulong, char>(ref reservedBuffer), sizeof(ulong) / sizeof(char));

        /// <summary>
        /// Indicates that decoders expected additional data to decode.
        /// </summary>
        public readonly bool NeedMoreData => reservedBufferSize > 0;

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

# if !NETSTANDARD2_1
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

        private void DecodeCore(ReadOnlySpan<char> chars, IBufferWriter<byte> output)
        {
            var size = chars.Length % 4;
            if (size > 0)
            {
                // size of the rest
                size = chars.Length - size;
                var rest = chars.Slice(size);
                rest.CopyTo(ReservedChars);
                reservedBufferSize = rest.Length; // keep the number of chars, not bytes
                chars = chars.Slice(0, size);
            }
            else
            {
                reservedBufferSize = 0;
            }

            // 4 characters => 3 bytes
            var buffer = output.GetSpan(chars.Length);
            if (!Convert.TryFromBase64Chars(chars, buffer, out size))
                throw new FormatException(ExceptionMessages.MalformedBase64);
            output.Advance(size);
        }

# if !NETSTANDARD2_1
        [SkipLocalsInit]
#endif
        private void CopyAndDecode(ReadOnlySpan<char> chars, IBufferWriter<byte> output)
        {
            var newSize = reservedBufferSize + chars.Length;
            using var tempBuffer = newSize <= MemoryRental<char>.StackallocThreshold ? stackalloc char[newSize] : new MemoryRental<char>(newSize);
            ReservedChars.Slice(0, reservedBufferSize).CopyTo(tempBuffer.Span);
            chars.CopyTo(tempBuffer.Span.Slice(reservedBufferSize));
            DecodeCore(tempBuffer.Span, output);
        }

        /// <summary>
        /// Decodes base64 characters.
        /// </summary>
        /// <param name="chars">The span containing base64-encoded bytes.</param>
        /// <param name="output">The output growable buffer used to write decoded bytes.</param>
        /// <exception cref="FormatException">The input base64 string is malformed.</exception>
        public void Decode(ReadOnlySpan<char> chars, IBufferWriter<byte> output)
        {
            if (reservedBufferSize > 0)
                CopyAndDecode(chars, output);
            else
                DecodeCore(chars, output);
        }

        /// <summary>
        /// Decodes base64 characters.
        /// </summary>
        /// <param name="chars">The span containing base64-encoded bytes.</param>
        /// <param name="output">The output growable buffer used to write decoded bytes.</param>
        /// <exception cref="FormatException">The input base64 string is malformed.</exception>
        public void Decode(in ReadOnlySequence<char> chars, IBufferWriter<byte> output)
        {
            foreach (var chunk in chars)
                Decode(chunk.Span, output);
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

# if !NETSTANDARD2_1
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

# if !NETSTANDARD2_1
        [SkipLocalsInit]
#endif
        private void DecodeCore<TArg>(ReadOnlySpan<char> chars, in ValueReadOnlySpanAction<byte, TArg> output, TArg arg)
        {
            const int maxInputBlockSize = 340; // 340 chars can be decoded as 255 bytes which is <= DecodingBufferSize
            Span<byte> buffer = stackalloc byte[DecodingBufferSize];

            consume_next_chunk:
            if (Decode(chars.TrimLength(maxInputBlockSize), buffer, out var consumed, out var produced))
            {
                reservedBufferSize = 0;
            }
            else
            {
                reservedBufferSize = chars.Length - consumed;
                Debug.Assert(reservedBufferSize <= 4);
                chars.Slice(consumed).CopyTo(ReservedChars);
            }

            if (consumed > 0 && produced > 0)
            {
                output.Invoke(buffer.Slice(0, produced), arg);
                chars = chars.Slice(consumed);
                goto consume_next_chunk;
            }

            // true - decoding completed, false - need more data
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static bool Decode(ReadOnlySpan<char> input, Span<byte> output, out int consumedChars, out int producedBytes)
            {
                Debug.Assert(output.Length == DecodingBufferSize);
                Debug.Assert(input.Length <= maxInputBlockSize);
                bool result;
                int rest;

                // x & 3 is the same as x % 4
                if (result = (rest = input.Length & 3) == 0)
                    consumedChars = input.Length;
                else
                    input = input.Slice(0, consumedChars = input.Length - rest);

                return Convert.TryFromBase64Chars(input, output, out producedBytes) ?
                    result :
                    throw new FormatException(ExceptionMessages.MalformedBase64);
            }
        }

        # if !NETSTANDARD2_1
        [SkipLocalsInit]
#endif
        private void CopyAndDecode<TArg>(ReadOnlySpan<char> chars, in ValueReadOnlySpanAction<byte, TArg> output, TArg arg)
        {
            var newSize = reservedBufferSize + chars.Length;
            using var tempBuffer = newSize <= MemoryRental<char>.StackallocThreshold ? stackalloc char[newSize] : new MemoryRental<char>(newSize);
            ReservedChars.Slice(0, reservedBufferSize).CopyTo(tempBuffer.Span);
            chars.CopyTo(tempBuffer.Span.Slice(reservedBufferSize));
            DecodeCore(tempBuffer.Span, in output, arg);
        }

        /// <summary>
        /// Decodes base64-encoded bytes.
        /// </summary>
        /// <typeparam name="TArg">The type of the argument to be passed to the callback.</typeparam>
        /// <param name="chars">The span containing base64-encoded bytes.</param>
        /// <param name="output">The callback called for decoded portion of data.</param>
        /// <param name="arg">The argument to be passed to the callback.</param>
        /// <exception cref="FormatException">The input base64 string is malformed.</exception>
        public void Decode<TArg>(ReadOnlySpan<char> chars, in ValueReadOnlySpanAction<byte, TArg> output, TArg arg)
        {
            if (reservedBufferSize > 0)
                CopyAndDecode(chars, in output, arg);
            else
                DecodeCore(chars, in output, arg);
        }

        /// <summary>
        /// Decodes base64-encoded bytes.
        /// </summary>
        /// <typeparam name="TArg">The type of the argument to be passed to the callback.</typeparam>
        /// <param name="chars">The span containing base64-encoded bytes.</param>
        /// <param name="output">The callback called for decoded portion of data.</param>
        /// <param name="arg">The argument to be passed to the callback.</param>
        /// <exception cref="FormatException">The input base64 string is malformed.</exception>
        public void Decode<TArg>(in ReadOnlySequence<char> chars, in ValueReadOnlySpanAction<byte, TArg> output, TArg arg)
        {
            foreach (var chunk in chars)
                Decode(chunk.Span, in output, arg);
        }
    }
}
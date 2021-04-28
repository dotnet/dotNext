using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Text
{
    using Buffers;

    public partial struct Base64Decoder
    {
        private Span<char> ReservedChars => MemoryMarshal.CreateSpan(ref Unsafe.As<ulong, char>(ref reservedBuffer), sizeof(ulong) / sizeof(char));

        private void DecodeCore(ReadOnlySpan<char> chars, IBufferWriter<byte> output)
        {
            var size = chars.Length & 3;
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

#if !NETSTANDARD2_1
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

#if !NETSTANDARD2_1
        [SkipLocalsInit]
#endif
        private void DecodeCore<TConsumer>(ReadOnlySpan<char> chars, TConsumer output)
            where TConsumer : notnull, IReadOnlySpanConsumer<byte>
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
                output.Invoke(buffer.Slice(0, produced));
                chars = chars.Slice(consumed);
                goto consume_next_chunk;
            }

            // true - decoding completed, false - need more data
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

#if !NETSTANDARD2_1
        [SkipLocalsInit]
#endif
        private void CopyAndDecode<TConsumer>(ReadOnlySpan<char> chars, TConsumer output)
            where TConsumer : notnull, IReadOnlySpanConsumer<byte>
        {
            var newSize = reservedBufferSize + chars.Length;
            using var tempBuffer = newSize <= MemoryRental<char>.StackallocThreshold ? stackalloc char[newSize] : new MemoryRental<char>(newSize);
            ReservedChars.Slice(0, reservedBufferSize).CopyTo(tempBuffer.Span);
            chars.CopyTo(tempBuffer.Span.Slice(reservedBufferSize));
            DecodeCore(tempBuffer.Span, output);
        }

        /// <summary>
        /// Decodes base64-encoded bytes.
        /// </summary>
        /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
        /// <param name="chars">The span containing base64-encoded bytes.</param>
        /// <param name="output">The consumer called for decoded portion of data.</param>
        /// <exception cref="FormatException">The input base64 string is malformed.</exception>
        public void Decode<TConsumer>(ReadOnlySpan<char> chars, TConsumer output)
            where TConsumer : notnull, IReadOnlySpanConsumer<byte>
        {
            if (reservedBufferSize > 0)
                CopyAndDecode(chars, output);
            else
                DecodeCore(chars, output);
        }

        /// <summary>
        /// Decodes base64-encoded bytes.
        /// </summary>
        /// <typeparam name="TArg">The type of the argument to be passed to the callback.</typeparam>
        /// <param name="chars">The span containing base64-encoded bytes.</param>
        /// <param name="callback">The callback called for decoded portion of data.</param>
        /// <param name="arg">The argument to be passed to the callback.</param>
        /// <exception cref="FormatException">The input base64 string is malformed.</exception>
        public void Decode<TArg>(ReadOnlySpan<char> chars, ReadOnlySpanAction<byte, TArg> callback, TArg arg)
            => Decode(chars, new DelegatingReadOnlySpanConsumer<byte, TArg>(callback, arg));

        /// <summary>
        /// Decodes base64-encoded bytes.
        /// </summary>
        /// <typeparam name="TArg">The type of the argument to be passed to the callback.</typeparam>
        /// <param name="chars">The span containing base64-encoded bytes.</param>
        /// <param name="callback">The callback called for decoded portion of data.</param>
        /// <param name="arg">The argument to be passed to the callback.</param>
        /// <exception cref="FormatException">The input base64 string is malformed.</exception>
        [CLSCompliant(false)]
        public unsafe void Decode<TArg>(ReadOnlySpan<char> chars, delegate*<ReadOnlySpan<byte>, TArg, void> callback, TArg arg)
            => Decode(chars, new ReadOnlySpanConsumer<byte, TArg>(callback, arg));
    }
}
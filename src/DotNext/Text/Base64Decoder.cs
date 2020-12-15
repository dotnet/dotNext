using System;
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Text
{
    using Buffers;

    /// <summary>
    /// Represents base64 decoder suitable for streaming.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public ref struct Base64Decoder
    {
        private int reservedBuffer; // 4 bytes buffer for decoding base64
        private int reservedBufferSize;

        private static void DecodeCore(ReadOnlySpan<byte> utf8Chars, IBufferWriter<byte> output, Span<byte> reservedBuffer, ref int reservedBufferSize)
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
                    utf8Chars.Slice(consumed).CopyTo(reservedBuffer);
                    break;
            }

            output.Advance(produced);
        }

# if !NETSTANDARD2_1
        [SkipLocalsInit]
#endif
        private static void CopyAndDecode(ReadOnlySpan<byte> utf8Chars, IBufferWriter<byte> output, Span<byte> reservedBuffer, ref int reservedBufferSize)
        {
            var newSize = reservedBufferSize + utf8Chars.Length;
            using var tempBuffer = newSize <= MemoryRental<byte>.StackallocThreshold ? stackalloc byte[newSize] : new MemoryRental<byte>(newSize);
            reservedBuffer.Slice(0, reservedBufferSize).CopyTo(tempBuffer.Span);
            utf8Chars.CopyTo(tempBuffer.Span.Slice(reservedBufferSize));
            DecodeCore(tempBuffer.Span, output, reservedBuffer, ref reservedBufferSize);
        }

        /// <summary>
        /// Decodes UTF-8 encoded base64 string.
        /// </summary>
        /// <param name="utf8Chars">UTF-8 encoded portion of base64 string.</param>
        /// <param name="output">The output growable buffer used to write decoded bytes.</param>
        /// <exception cref="FormatException">The input base64 string is malformed.</exception>
        public void Decode(ReadOnlySpan<byte> utf8Chars, IBufferWriter<byte> output)
        {
            Span<byte> reservedBuffer = Span.AsBytes(ref this.reservedBuffer);

            if (reservedBufferSize > 0)
                CopyAndDecode(utf8Chars, output, reservedBuffer, ref reservedBufferSize);
            else
                DecodeCore(utf8Chars, output, reservedBuffer, ref reservedBufferSize);
        }

# if !NETSTANDARD2_1
        [SkipLocalsInit]
#endif
        private static void DecodeCore<TArg>(ReadOnlySpan<byte> utf8Chars, in ValueReadOnlySpanAction<byte, TArg> output, TArg arg, Span<byte> reservedBuffer, ref int reservedBufferSize)
        {
            var produced = Base64.GetMaxDecodedFromUtf8Length(utf8Chars.Length);
            using var buffer = produced <= MemoryRental<byte>.StackallocThreshold ? stackalloc byte[produced] : new MemoryRental<byte>(produced);

            // x & 3 is the same as x % 4
            switch (Base64.DecodeFromUtf8(utf8Chars, buffer.Span, out var consumed, out produced, (utf8Chars.Length & 3) == 0))
            {
                case OperationStatus.Done:
                    reservedBufferSize = 0;
                    break;
                case OperationStatus.InvalidData:
                    throw new FormatException(ExceptionMessages.MalformedBase64);
                default:
                    reservedBufferSize = utf8Chars.Length - consumed;
                    Debug.Assert(reservedBufferSize <= 4);
                    utf8Chars.Slice(consumed).CopyTo(reservedBuffer);
                    break;
            }

            output.Invoke(buffer.Span.Slice(0, produced), arg);
        }

# if !NETSTANDARD2_1
        [SkipLocalsInit]
#endif
        private static void CopyAndDecode<TArg>(ReadOnlySpan<byte> utf8Chars, in ValueReadOnlySpanAction<byte, TArg> output, TArg arg, Span<byte> reservedBuffer, ref int reservedBufferSize)
        {
            var newSize = reservedBufferSize + utf8Chars.Length;
            using var tempBuffer = newSize <= MemoryRental<byte>.StackallocThreshold ? stackalloc byte[newSize] : new MemoryRental<byte>(newSize);
            reservedBuffer.Slice(0, reservedBufferSize).CopyTo(tempBuffer.Span);
            utf8Chars.CopyTo(tempBuffer.Span.Slice(reservedBufferSize));
            DecodeCore(tempBuffer.Span, in output, arg, reservedBuffer, ref reservedBufferSize);
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
            Span<byte> reservedBuffer = Span.AsBytes(ref this.reservedBuffer);

            if (reservedBufferSize > 0)
                CopyAndDecode(utf8Chars, in output, arg, reservedBuffer, ref reservedBufferSize);
            else
                DecodeCore(utf8Chars, in output, arg, reservedBuffer, ref reservedBufferSize);
        }
    }
}
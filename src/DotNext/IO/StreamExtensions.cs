using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using Buffers;
    using Text;
    using Memory = Runtime.InteropServices.Memory;

    /// <summary>
    /// Represents high-level read/write methods for the stream.
    /// </summary>
    public static class StreamExtensions
    {
        private static unsafe int GetBytes(this Encoder encoder, string value, int charOffset, int charCount, byte[] buffer, bool flush)
        {
            fixed (char* pChars = value)
            fixed (byte* pBytes = buffer)
                return encoder.GetBytes(pChars + charOffset, charCount, pBytes, buffer.Length, flush);
        }

        /// <summary>
        /// Writes the string into the stream.
        /// </summary>
        /// <remarks>
        /// This method doesn't encode the length of the string.
        /// </remarks>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="value">The string to be encoded.</param>
        /// <param name="context">The encoding.</param>
        /// <param name="buffer">The buffer allocated by the caller needed for characters encoding.</param>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> is too small for encoding.</exception>
        public static unsafe void WriteString(this Stream stream, string value, EncodingContext context, byte[] buffer)
        {
            if(value.Length == 0)
                return;
            //TODO: Should be rewritten for .NET Standard 2.1
            if(context.Encoding.GetByteCount(value) <= buffer.Length)
                stream.Write(buffer, 0, context.Encoding.GetBytes(value, 0, value.Length, buffer, 0));
            else
            {
                var maxChars = buffer.Length / context.Encoding.GetMaxByteCount(1);
                if(maxChars == 0)
                    throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));
                var encoder = context.GetEncoder();
                for(int charStart = 0, numLeft = value.Length, charsRead; numLeft > 0; charStart += charsRead, numLeft -= charsRead)
                {
                    charsRead = Math.Min(numLeft, maxChars);
                    stream.Write(buffer, 0, encoder.GetBytes(value, charStart, charsRead, buffer, charsRead == numLeft));
                }
            }
        }

        /// <summary>
        /// Writes the string into the stream asynchronously.
        /// </summary>
        /// <remarks>
        /// This method doesn't encode the length of the string.
        /// </remarks>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="value">The string to be encoded.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="buffer">The buffer allocated by the caller needed for characters encoding.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> is too small for encoding.</exception>
        public static async Task WriteStringAsync(this Stream stream, string value, EncodingContext context, byte[] buffer, CancellationToken token = default)
        {
            if(value.Length == 0)
                return;
            //TODO: Should be rewritten for .NET Standard 2.1
            if(context.Encoding.GetByteCount(value) <= buffer.Length)
                await stream.WriteAsync(buffer, 0, context.Encoding.GetBytes(value, 0, value.Length, buffer, 0), token).ConfigureAwait(false);
            else
            {
                var maxChars = buffer.Length / context.Encoding.GetMaxByteCount(1);
                if(maxChars == 0)
                    throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));
                var encoder = context.GetEncoder();
                for(int charStart = 0, numLeft = value.Length, charsRead; numLeft > 0; charStart += charsRead, numLeft -= charsRead)
                {
                    charsRead = Math.Min(numLeft, maxChars);
                    await stream.WriteAsync(buffer, 0, encoder.GetBytes(value, charStart, charsRead, buffer, charsRead == numLeft), token).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Reads the string using the specified encoding.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="length">The length of the string, in bytes.</param>
        /// <param name="context">The text decoding context.</param>
        /// <returns>The string decoded from the log entry content stream.</returns>
        public static string ReadString(this Stream stream, byte[] buffer, int length, DecodingContext context)
        {
            //TODO: Should be rewritten for .NET Standard 2.1
            var maxChars = context.Encoding.GetMaxCharCount(buffer.Length);
            if(maxChars == 0)
                throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));
            var decoder = context.GetDecoder();
            var charBuffer = new ArrayRental<char>(maxChars);
            var result = default(ArrayRental<char>);
            int currentPos = 0, resultOffset = 0;
            try
            {
                do
                {
                    var readLength = Math.Min(length - currentPos, buffer.Length);
                    var n = stream.Read(buffer, 0, readLength);
                    if (n == 0)
                        throw new EndOfStreamException();
                    var charsRead = decoder.GetChars(buffer, 0, n, charBuffer, 0);
                    if (currentPos == 0 && n == length)
                        return new string(charBuffer, 0, charsRead);
                    if (result.IsEmpty)
                        result = new ArrayRental<char>(length);
                    Memory.Copy(ref charBuffer[0], ref result[resultOffset], (uint)charsRead);
                    resultOffset += charsRead;
                    currentPos += n;
                }
                while (currentPos < length);
                return new string(result, 0, resultOffset);
            }
            finally
            {
                result.Dispose();
            }
        }

        /// <summary>
        /// Reads the string asynchronously using the specified encoding.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="length">The length of the string.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The string decoded from the log entry content stream.</returns>
        public static async Task<string> ReadStringAsync(this Stream stream, byte[] buffer, int length, DecodingContext context, CancellationToken token = default)
        {
            //TODO: Should be rewritten for .NET Standard 2.1
            var maxChars = context.Encoding.GetMaxCharCount(buffer.Length);
            if(maxChars == 0)
                throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));
            var decoder = context.GetDecoder();
            var charBuffer = new ArrayRental<char>(maxChars);
            var result = default(ArrayRental<char>);
            int currentPos = 0, resultOffset = 0;
            try
            {
                do
                {
                    var readLength = Math.Min(length - currentPos, buffer.Length);
                    var n = await stream.ReadAsync(buffer, 0, readLength, token).ConfigureAwait(false);
                    if (n == 0)
                        throw new EndOfStreamException();
                    var charsRead = decoder.GetChars(buffer, 0, n, charBuffer, 0);
                    if (currentPos == 0 && n == length)
                        return new string(charBuffer, 0, charsRead);
                    if (result.IsEmpty)
                        result = new ArrayRental<char>(length);
                    Memory.Copy(ref charBuffer[0], ref result[resultOffset], (uint)charsRead);
                    resultOffset += charsRead;
                    currentPos += n;
                }
                while (currentPos < length);
                return new string(result, 0, resultOffset);
            }
            finally
            {
                result.Dispose();
            }
        }

        /// <summary>
        /// Reads the number of bytes using the pre-allocated buffer.
        /// </summary>
        /// <remarks>
        /// You can use <see cref="System.Buffers.Binary.BinaryPrimitives"/> to decode the returned bytes.
        /// </remarks>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>The span of bytes representing buffer segment.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is greater than the length of <paramref name="buffer"/>.</exception>
        /// <exception cref="EndOfStreamException">End of stream is reached.</exception>
        public static ReadOnlySpan<byte> ReadBytes(this Stream stream, byte[] buffer, int count)
        {
            if (count == 0)
                return default;
            if (count > buffer.LongLength)
                throw new ArgumentOutOfRangeException(nameof(count));
            var bytesRead = 0;
            do
            {
                var n = stream.Read(buffer, bytesRead, count - bytesRead);
                if (n == 0)
                    throw new EndOfStreamException();
                bytesRead += n;
            } while (bytesRead < count);
            return new ReadOnlySpan<byte>(buffer, 0, bytesRead);
        }

        /// <summary>
        /// Reads asynchronously the number of bytes using the pre-allocated buffer.
        /// </summary>
        /// <remarks>
        /// You can use <see cref="System.Buffers.Binary.BinaryPrimitives"/> to decode the returned bytes.
        /// </remarks>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The span of bytes representing buffer segment.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is greater than the length of <paramref name="buffer"/>.</exception>
        /// <exception cref="EndOfStreamException">End of stream is reached.</exception>
        public static async Task<ReadOnlyMemory<byte>> ReadBytesAsync(this Stream stream, byte[] buffer, int count, CancellationToken token = default)
        {
            if (count == 0)
                return default;
            if (count > buffer.LongLength)
                throw new ArgumentOutOfRangeException(nameof(count));
            var bytesRead = 0;
            do
            {
                var n = await stream.ReadAsync(buffer, bytesRead, count - bytesRead, token).ConfigureAwait(false);
                if (n == 0)
                    throw new EndOfStreamException();
                bytesRead += n;
            } while (bytesRead < count);
            return new ReadOnlyMemory<byte>(buffer, 0, bytesRead);
        }
    }
}
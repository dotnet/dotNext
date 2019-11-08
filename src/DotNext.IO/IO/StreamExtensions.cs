using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using static System.Diagnostics.Debug;
using static System.Runtime.CompilerServices.Unsafe;
using MemoryMarshal = System.Runtime.InteropServices.MemoryMarshal;

namespace DotNext.IO
{
    using Buffers;
    using Text;
    using Memory = Runtime.InteropServices.Memory;

    /// <summary>
    /// Represents high-level read/write methods for the stream.
    /// </summary>
    /// <remarks>
    /// This class provides alternative way to read and write typed data from/to the stream
    /// without instantiation of <see cref="BinaryReader"/> and <see cref="BinaryWriter"/>.
    /// </remarks>
    public static class StreamExtensions
    {
        public static async ValueTask<long> ReadAsync(this Stream source, PipeWriter destination, int bufferSize = 0, CancellationToken token = default)
        {
            var total = 0L;
            for (int bytesRead; ; token.ThrowIfCancellationRequested())
            {
                bytesRead = await source.ReadAsync(destination.GetMemory(bufferSize), token).ConfigureAwait(false);
                destination.Advance(bytesRead);
                if (bytesRead == 0)
                    break;
                total += bytesRead;
                var result = await destination.FlushAsync().ConfigureAwait(false);
                if (result.IsCanceled)
                    throw new OperationCanceledException(token.IsCancellationRequested ? token : new CancellationToken(true));
                if (result.IsCompleted)
                    break;
            }
            return total;
        }

        public static async ValueTask<long> WriteAsync(this Stream destination, PipeReader source, CancellationToken token = default)
        {
            var total = 0L;
            for (SequencePosition consumed = default; ; consumed = default)
                try
                {
                    var result = await source.ReadAsync(token).ConfigureAwait(false);
                    var buffer = result.Buffer;
                    if (result.IsCanceled)
                        throw new OperationCanceledException(token.IsCancellationRequested ? token : new CancellationToken(true));
                    for (var position = buffer.Start; buffer.TryGet(ref position, out var block); consumed = position, total += block.Length)
                        await destination.WriteAsync(block, token).ConfigureAwait(false);
                    if (consumed.Equals(default))
                        consumed = buffer.End;
                    if (result.IsCompleted)
                        break;
                }
                finally
                {
                    source.AdvanceTo(consumed);
                }
            return total;
        }

        /// <summary>
        /// Writes the string into the stream.
        /// </summary>
        /// <remarks>
        /// This method doesn't encode the length of the string.
        /// </remarks>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The string to be encoded.</param>
        /// <param name="context">The encoding.</param>
        /// <param name="buffer">The buffer allocated by the caller needed for characters encoding.</param>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> is too small for encoding.</exception>
        public static void WriteString(this Stream stream, ReadOnlySpan<char> value, EncodingContext context, Span<byte> buffer)
        {
            if (value.Length == 0)
                return;
            var encoder = context.GetEncoder();
            var completed = false;
            for (int offset = 0, charsUsed; !completed; offset += charsUsed)
            {
                var chars = value.Slice(offset);
                encoder.Convert(chars, buffer, chars.Length == 0, out charsUsed, out var bytesUsed, out completed);
                stream.Write(buffer.Slice(0, bytesUsed));
                value = chars;
            }
        }

        /// <summary>
        /// Writes the string into the stream asynchronously.
        /// </summary>
        /// <remarks>
        /// This method doesn't encode the length of the string.
        /// </remarks>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The string to be encoded.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="buffer">The buffer allocated by the caller needed for characters encoding.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> is too small for encoding.</exception>
        public static async ValueTask WriteStringAsync(this Stream stream, ReadOnlyMemory<char> value, EncodingContext context, Memory<byte> buffer, CancellationToken token = default)
        {
            if (value.Length == 0)
                return;
            var encoder = context.GetEncoder();
            var completed = false;
            for (int offset = 0, charsUsed; !completed; offset += charsUsed)
            {
                var chars = value.Slice(offset);
                encoder.Convert(chars.Span, buffer.Span, chars.Length == 0, out charsUsed, out var bytesUsed, out completed);
                await stream.WriteAsync(buffer.Slice(0, bytesUsed), token).ConfigureAwait(false);
                value = chars;
            }
        }

        /// <summary>
        /// Reads the string using the specified encoding.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="length">The length of the string, in bytes.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <returns>The string decoded from the log entry content stream.</returns>
        public static string ReadString(this Stream stream, int length, DecodingContext context, Span<byte> buffer)
        {
            var maxChars = context.Encoding.GetMaxCharCount(buffer.Length);
            if (maxChars == 0)
                throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));
            var decoder = context.GetDecoder();
            using var charBuffer = maxChars <= 1024 ? stackalloc char[maxChars] : new MemoryRental<char>(maxChars);
            using var result = length <= 1024 ? stackalloc char[length] : new MemoryRental<char>(length);
            int resultOffset;
            for (resultOffset = 0; length > 0;)
            {
                var n = stream.Read(buffer.Slice(0, Math.Min(length, buffer.Length)));
                if (n == 0)
                    throw new EndOfStreamException();
                length -= n;
                var charsRead = decoder.GetChars(buffer.Slice(0, n), charBuffer.Span, length == 0);
                Memory.Copy(ref charBuffer[0], ref result[resultOffset], (uint)charsRead);
                resultOffset += charsRead;
            }
            return new string(result.Span.Slice(0, resultOffset));
        }

        /// <summary>
        /// Reads the string asynchronously using the specified encoding.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="length">The length of the string.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The string decoded from the log entry content stream.</returns>
        public static async ValueTask<string> ReadStringAsync(this Stream stream, int length, DecodingContext context, Memory<byte> buffer, CancellationToken token = default)
        {
            var maxChars = context.Encoding.GetMaxCharCount(buffer.Length);
            if (maxChars == 0)
                throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));
            var decoder = context.GetDecoder();
            using var continuousBuffer = new ArrayRental<char>(maxChars + length);
            var charBuffer = continuousBuffer.Memory.Slice(0, maxChars);
            var result = continuousBuffer.Memory.Slice(maxChars);
            Assert(result.Length == length);
            int resultOffset;
            for (resultOffset = 0; length > 0;)
            {
                var n = await stream.ReadAsync(buffer.Slice(0, Math.Min(length, buffer.Length))).ConfigureAwait(false);
                if (n == 0)
                    throw new EndOfStreamException();
                length -= n;
                var charsRead = decoder.GetChars(buffer.Span.Slice(0, n), charBuffer.Span, length == 0);
                Memory.Copy(ref charBuffer.Span[0], ref result.Span[resultOffset], (uint)charsRead);
                resultOffset += charsRead;
            }
            return new string(result.Span.Slice(0, resultOffset));
        }

        /// <summary>
        /// Deserializes the value type from the stream.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <typeparam name="T">The value type to be deserialized.</typeparam>
        /// <returns>The value deserialized from the stream.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
        public static unsafe T Read<T>(this Stream stream)
            where T : unmanaged
        {
            var result = default(T);
            return stream.Read(Memory.AsSpan(&result)) == sizeof(T) ? result : throw new EndOfStreamException();
        }

        /// <summary>
        /// Asynchronously deserializes the value type from the stream.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <typeparam name="T">The value type to be deserialized.</typeparam>
        /// <returns>The value deserialized from the stream.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
        public static async ValueTask<T> ReadAsync<T>(this Stream stream, Memory<byte> buffer, CancellationToken token = default)
            where T : unmanaged
        {
            var bytesRead = await stream.ReadAsync(buffer.Slice(0, SizeOf<T>()), token).ConfigureAwait(false);
            return bytesRead == SizeOf<T>() ? MemoryMarshal.Read<T>(buffer.Span) : throw new EndOfStreamException();
        }

        /// <summary>
        /// Asynchronously deserializes the value type from the stream.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <typeparam name="T">The value type to be deserialized.</typeparam>
        /// <returns>The value deserialized from the stream.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
        public static async ValueTask<T> ReadAsync<T>(this Stream stream, CancellationToken token = default)
            where T : unmanaged
        {
            using var buffer = new ArrayRental<byte>(SizeOf<T>());
            return await ReadAsync<T>(stream, buffer.Memory, token);
        }

        /// <summary>
        /// Serializes value to the stream.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to be written into the stream.</param>
        /// <typeparam name="T">The value type to be serialized.</typeparam>
        public static unsafe void Write<T>(this Stream stream, ref T value) where T : unmanaged => stream.Write(Memory.AsSpan(ref value));

        /// <summary>
        /// Asynchronously serializes value to the stream.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to be written into the stream.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <typeparam name="T">The value type to be serialized.</typeparam>
        /// <returns>The task representing asynchronous st</returns>
        public static ValueTask WriteAsync<T>(this Stream stream, ref T value, Memory<byte> buffer, CancellationToken token = default)
            where T : unmanaged
        {
            MemoryMarshal.Write(buffer.Span, ref value);
            return stream.WriteAsync(buffer.Slice(0, SizeOf<T>()), token);
        }

        /// <summary>
        /// Asynchronously serializes value to the stream.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to be written into the stream.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <typeparam name="T">The value type to be serialized.</typeparam>
        /// <returns>The task representing asynchronous st</returns>
        public static ValueTask WriteAsync<T>(this Stream stream, T value, CancellationToken token = default)
            where T : unmanaged
        {
            using var buffer = new ArrayRental<byte>(SizeOf<T>());
            MemoryMarshal.Write(buffer.Span, ref value);
            return stream.WriteAsync(buffer.Memory, token);
        }

        /// <summary>
        /// Asynchronously reads the bytes from the source stream and writes them to another stream, using a specified buffer.
        /// </summary>
        /// <param name="source">The source stream to read from.</param>
        /// <param name="destination">The destination stream to write into.</param>
        /// <param name="buffer">The buffer used to hold copied content temporarily.</param>
        /// <param name="token">The token that can be used to cancel this operation.</param>
        /// <returns>The total number of copied bytes.</returns>
        public static async Task<long> CopyToAsync(this Stream source, Stream destination, Memory<byte> buffer, CancellationToken token = default)
        {
            var totalBytes = 0L;
            int count;
            while ((count = await source.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
            {
                totalBytes += count;
                await destination.WriteAsync(buffer.Slice(0, count), token).ConfigureAwait(false);
            }
            return totalBytes;
        }

        /// <summary>
        /// Synchronously reads the bytes from the source stream and writes them to another stream, using a specified buffer.
        /// </summary>
        /// <param name="source">The source stream to read from.</param>
        /// <param name="destination">The destination stream to write into.</param>
        /// <param name="buffer">The buffer used to hold copied content temporarily.</param>
        /// <param name="token">The token that can be used to cancel this operation.</param>
        /// <returns>The total number of copied bytes.</returns>
        public static long CopyTo(this Stream source, Stream destination, Span<byte> buffer, CancellationToken token = default)
        {
            var totalBytes = 0L;
            int count;
            while ((count = source.Read(buffer)) > 0)
            {
                totalBytes += count;
                token.ThrowIfCancellationRequested();
                destination.Write(buffer.Slice(0, count));
            }
            return totalBytes;
        }
    }
}

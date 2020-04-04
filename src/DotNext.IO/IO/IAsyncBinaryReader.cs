using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using DecodingContext = Text.DecodingContext;
    using static Buffers.BufferReader;

    /// <summary>
    /// Providers a uniform way to decode the data
    /// from various sources such as streams, pipes, unmanaged memory etc.
    /// </summary>
    /// <seealso cref="IAsyncBinaryWriter"/>
    public interface IAsyncBinaryReader
    {
        /// <summary>
        /// Represents empty reader.
        /// </summary>
        public static IAsyncBinaryReader Empty { get; } = new EmptyBinaryReader();

        /// <summary>
        /// Decodes the value of blittable type.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <returns>The decoded value.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        ValueTask<T> ReadAsync<T>(CancellationToken token = default) where T : unmanaged;

        /// <summary>
        /// Decodes 64-bit signed integer using the specified endianness.
        /// </summary>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        async ValueTask<long> ReadInt64Async(bool littleEndian, CancellationToken token = default)
        {
            var result = await ReadAsync<long>(token).ConfigureAwait(false);
            result.ReverseIfNeeded(littleEndian);
            return result;
        }

        /// <summary>
        /// Decodes 32-bit signed integer using the specified endianness.
        /// </summary>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>        
        async ValueTask<int> ReadInt32Async(bool littleEndian, CancellationToken token = default)
        {
            var result = await ReadAsync<int>(token).ConfigureAwait(false);
            result.ReverseIfNeeded(littleEndian);
            return result;
        }
        
        /// <summary>
        /// Decodes 16-bit signed integer using the specified endianness.
        /// </summary>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        async ValueTask<short> ReadInt16Async(bool littleEndian, CancellationToken token = default)
        {
            var result = await ReadAsync<short>(token).ConfigureAwait(false);
            result.ReverseIfNeeded(littleEndian);
            return result;
        }

        /// <summary>
        /// Reads the block of bytes.
        /// </summary>
        /// <param name="output">The block of memory to fill.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        ValueTask ReadAsync(Memory<byte> output, CancellationToken token = default);

        /// <summary>
        /// Decodes the string.
        /// </summary>
        /// <param name="length">The length of the encoded string, in bytes.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The decoded string.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        ValueTask<string> ReadStringAsync(int length, DecodingContext context, CancellationToken token = default);

        /// <summary>
        /// Decodes the string.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The decoded string.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        ValueTask<string> ReadStringAsync(StringLengthEncoding lengthFormat, DecodingContext context, CancellationToken token = default);

        /// <summary>
        /// Copies the content to the specified stream.
        /// </summary>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <param name="output">The output stream receiving object content.</param>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        Task CopyToAsync(Stream output, CancellationToken token = default);

        /// <summary>
        /// Copies the content to the specified pipe writer.
        /// </summary>
        /// <param name="output">The writer.</param>
        /// <param name="token">The token that can be used to cancel operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        Task CopyToAsync(PipeWriter output, CancellationToken token = default);

        /// <summary>
        /// Creates default implementation of binary reader for the stream.
        /// </summary>
        /// <remarks>
        /// It is recommended to use extension methods from <see cref="StreamExtensions"/> class
        /// for decoding data from the stream. This method is intended for situation
        /// when you need an object implementing <see cref="IAsyncBinaryReader"/> interface.
        /// </remarks>
        /// <param name="input">The stream to be wrapped into the reader.</param>
        /// <param name="buffer">The buffer used for decoding data from the stream.</param>
        /// <returns>The stream reader.</returns>
        public static IAsyncBinaryReader Create(Stream input, Memory<byte> buffer) => new AsyncStreamBinaryReader(input, buffer);

        /// <summary>
        /// Creates default implementation of binary reader over sequence of bytes.
        /// </summary>
        /// <param name="sequence">The sequence of bytes.</param>
        /// <returns>The binary reader for the sequence of bytes.</returns>
        public static SequenceBinaryReader Create(ReadOnlySequence<byte> sequence) => new SequenceBinaryReader(sequence);

        /// <summary>
        /// Creates default implementation of binary reader over contiguous memory block. 
        /// </summary>
        /// <param name="memory">The block of memory.</param>
        /// <returns>The binary reader for the memory block.</returns>
        public static SequenceBinaryReader Create(ReadOnlyMemory<byte> memory) => Create(new ReadOnlySequence<byte>(memory));

        /// <summary>
        /// Creates default implementation of binary reader for the specifed pipe reader.
        /// </summary>
        /// <remarks>
        /// It is recommended to use extension methods from <see cref="Pipelines.PipeExtensions"/> class
        /// for decoding data from the stream. This method is intended for situation
        /// when you need an object implementing <see cref="IAsyncBinaryReader"/> interface.
        /// </remarks>
        /// <param name="reader">The pipe reader.</param>
        /// <returns>The binary reader.</returns>
        public static IAsyncBinaryReader Create(PipeReader reader) => new Pipelines.PipeBinaryReader(reader);
    }
}
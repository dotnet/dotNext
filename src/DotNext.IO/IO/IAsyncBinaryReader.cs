using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using DecodingContext = Text.DecodingContext;

    /// <summary>
    /// Provides uniform way to decode the data
    /// from various sources such as streams, pipes, unmanaged memory etc.
    /// </summary>
    public interface IAsyncBinaryReader
    {
        /// <summary>
        /// Decodes the value of blittable type.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <returns>The decoded value.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        ValueTask<T> ReadAsync<T>(CancellationToken token = default) where T : unmanaged;

        ValueTask ReadAsync(Memory<byte> output, CancellationToken token = default);

        ValueTask<string> ReadStringAsync(int length, DecodingContext context, CancellationToken token = default);

        ValueTask<string> ReadStringAsync(StringLengthEncoding lengthEncoding, DecodingContext context, CancellationToken token = default);
    
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
    }
}
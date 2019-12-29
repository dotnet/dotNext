using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using ByteBuffer = Buffers.ArrayRental<byte>;

    /// <summary>
    /// Represents structured data unit that can be transferred over wire.
    /// </summary>
    /// <seealso cref="IAsyncBinaryReader"/>
    /// <seealso cref="IAsyncBinaryWriter"/>
    public interface IDataTransferObject
    {
        /// <summary>
        /// Represents parser of DTO content.
        /// </summary>
        /// <typeparam name="TResult">The result type.</typeparam>
        public interface IDecoder<TResult>
        {
            /// <summary>
            /// Parses DTO content asynchronously.
            /// </summary>
            /// <param name="reader">The reader of DTO content.</param>
            /// <param name="token">The token that can be used to cancel the operation.</param>
            /// <typeparam name="TReader">The type of binary reader.</typeparam>
            /// <returns>The converted DTO content.</returns>
            ValueTask<TResult> DecodeAsync<TReader>(TReader reader, CancellationToken token)
                where TReader : IAsyncBinaryReader;
        }

        /// <summary>
        /// Indicates that the content of this object can be copied to the output stream or pipe multiple times.
        /// </summary>
        bool IsReusable { get; }

        /// <summary>
        /// Gets length of the object payload, in bytes.
        /// </summary>
        /// <remarks>
        /// If value is <see langword="null"/> then length of the payload cannot be determined.
        /// </remarks>
        long? Length { get; }

        /// <summary>
        /// Copies the object content into the specified stream.
        /// </summary>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <param name="output">The output stream receiving object content.</param>
        ValueTask CopyToAsync(Stream output, CancellationToken token = default);

        /// <summary>
        /// Copies the object content into the specified pipe writer.
        /// </summary>
        /// <param name="output">The writer.</param>
        /// <param name="token">The token that can be used to cancel operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        ValueTask CopyToAsync(PipeWriter output, CancellationToken token = default);

        /// <summary>
        /// Parses the content of binary transfer object. 
        /// </summary>
        /// <remarks>
        /// The default implementation copies the content into memory
        /// before parsing.
        /// </remarks>
        /// <param name="parser">The parser instance.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <typeparam name="TResult">The type of result.</typeparam>
        /// <typeparam name="TParser">The type of parser.</typeparam>
        /// <returns>The converted DTO content.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        async ValueTask<TResult> DecodeAsync<TResult, TParser>(TParser parser, CancellationToken token = default)
            where TParser : IDecoder<TResult>
        {
            const int bufferSize = 1024;
            using var ms = Length.TryGetValue(out var length) && length <= int.MaxValue ?
                new RentedMemoryStream((int)length) :
                new MemoryStream(bufferSize);
            await CopyToAsync(ms, token).ConfigureAwait(false);
            using var buffer = new ByteBuffer(bufferSize);
            ms.Position = 0;
            return await parser.DecodeAsync(new AsyncStreamBinaryReader(ms, buffer.Memory), token).ConfigureAwait(false);
        }
    }
}

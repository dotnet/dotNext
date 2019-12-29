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
        /// Represents DTO transformation.
        /// </summary>
        /// <typeparam name="TResult">The result type.</typeparam>
        public interface ITransformation<TResult>
        {
            /// <summary>
            /// Parses DTO content asynchronously.
            /// </summary>
            /// <param name="reader">The reader of DTO content.</param>
            /// <param name="token">The token that can be used to cancel the operation.</param>
            /// <typeparam name="TReader">The type of binary reader.</typeparam>
            /// <returns>The converted DTO content.</returns>
            ValueTask<TResult> TransformAsync<TReader>(TReader reader, CancellationToken token)
                where TReader : notnull, IAsyncBinaryReader;
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
        /// Transforms this object to serialized form.
        /// </summary>
        /// <param name="writer">The binary writer.</param>
        /// <param name="token">The toke that can be used to cancel the operation.</param>
        /// <typeparam name="TWriter">The type of writer.</typeparam>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        ValueTask TransformAsync<TWriter>(TWriter writer, CancellationToken token)
            where TWriter : IAsyncBinaryWriter;
        
        /// <summary>
        /// Decodes the stream.
        /// </summary>
        /// <param name="input">The stream to decode.</param>
        /// <param name="transformation">The decoder.</param>
        /// <param name="resetStream"><see langword="true"/> to reset stream position after decoding.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <typeparam name="TResult">The type of result.</typeparam>
        /// <typeparam name="TDecoder">The type of parser.</typeparam>
        /// <returns>The decoded stream.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        protected static async ValueTask<TResult> TransformAsync<TResult, TDecoder>(Stream input, TDecoder transformation, bool resetStream, CancellationToken token)
            where TDecoder : notnull, ITransformation<TResult>
        {
            const int bufferSize = 1024;
            var buffer = new ByteBuffer(bufferSize);
            try
            {
                return await transformation.TransformAsync(new AsyncStreamBinaryReader(input, buffer.Memory), token).ConfigureAwait(false);
            }
            finally
            {
                buffer.Dispose();
                if(resetStream && input.CanSeek)
                    input.Seek(0L, SeekOrigin.Begin);
            }
        }

        /// <summary>
        /// Decodes the data using pipe reader.
        /// </summary>
        /// <param name="input">The pipe reader used for decoding.</param>
        /// <param name="transformation">The decoder.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <typeparam name="TResult">The type of result.</typeparam>
        /// <typeparam name="TDecoder">The type of parser.</typeparam>
        /// <returns>The decoded stream.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        protected static ValueTask<TResult> TransformAsync<TResult, TDecoder>(PipeReader input, TDecoder transformation, CancellationToken token)
            where TDecoder : notnull, ITransformation<TResult>
            => transformation.TransformAsync(new Pipelines.PipeBinaryReader(input), token);

        /// <summary>
        /// Converts data transfer object to another type.
        /// </summary>
        /// <remarks>
        /// The default implementation copies the content into memory
        /// before parsing.
        /// </remarks>
        /// <param name="parser">The parser instance.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <typeparam name="TResult">The type of result.</typeparam>
        /// <typeparam name="TDecoder">The type of parser.</typeparam>
        /// <returns>The converted DTO content.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        async ValueTask<TResult> TransformAsync<TResult, TDecoder>(TDecoder parser, CancellationToken token = default)
            where TDecoder : notnull, ITransformation<TResult>
        {
            const int bufferSize = 1024;
            using var ms = Length.TryGetValue(out var length) && length <= int.MaxValue ?
                new RentedMemoryStream((int)length) :
                new MemoryStream(bufferSize);
            using var buffer = new ByteBuffer(bufferSize);
            await TransformAsync(new AsyncStreamBinaryWriter(ms, buffer.Memory), token).ConfigureAwait(false);
            ms.Position = 0;
            return await parser.TransformAsync(new AsyncStreamBinaryReader(ms, buffer.Memory), token).ConfigureAwait(false);
        }
    }
}

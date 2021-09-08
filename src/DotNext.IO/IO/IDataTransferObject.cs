using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using Buffers;

    /// <summary>
    /// Represents structured data unit that can be transferred over wire.
    /// </summary>
    /// <seealso cref="IAsyncBinaryReader"/>
    /// <seealso cref="IAsyncBinaryWriter"/>
    public interface IDataTransferObject
    {
        private const int DefaultBufferSize = 256;

        /// <summary>
        /// Gets empty data transfer object.
        /// </summary>
        public static IDataTransferObject Empty => EmptyDataTransferObject.Instance;

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
        ValueTask WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            where TWriter : IAsyncBinaryWriter;

        private static void ResetStream(Stream stream, bool resetStream)
        {
            if (resetStream && stream.CanSeek)
                stream.Seek(0L, SeekOrigin.Begin);
        }

        /// <summary>
        /// Decodes the stream.
        /// </summary>
        /// <param name="input">The stream to decode.</param>
        /// <param name="transformation">The decoder.</param>
        /// <param name="resetStream"><see langword="true"/> to reset stream position after decoding.</param>
        /// <param name="buffer">The temporary buffer.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <typeparam name="TResult">The type of result.</typeparam>
        /// <typeparam name="TTransformation">The type of parser.</typeparam>
        /// <returns>The decoded stream.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> is empty.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        protected static async ValueTask<TResult> TransformAsync<TResult, TTransformation>(Stream input, TTransformation transformation, bool resetStream, Memory<byte> buffer, CancellationToken token)
            where TTransformation : notnull, ITransformation<TResult>
        {
            if (buffer.IsEmpty)
                throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));

            try
            {
                return await transformation.TransformAsync(new AsyncStreamBinaryAccessor(input, buffer), token).ConfigureAwait(false);
            }
            finally
            {
                ResetStream(input, resetStream);
            }
        }

        /// <summary>
        /// Decodes the stream.
        /// </summary>
        /// <param name="input">The stream to decode.</param>
        /// <param name="transformation">The decoder.</param>
        /// <param name="resetStream"><see langword="true"/> to reset stream position after decoding.</param>
        /// <param name="allocator">The allocator of temporary buffer.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <typeparam name="TResult">The type of result.</typeparam>
        /// <typeparam name="TTransformation">The type of parser.</typeparam>
        /// <returns>The decoded stream.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        protected static async ValueTask<TResult> TransformAsync<TResult, TTransformation>(Stream input, TTransformation transformation, bool resetStream, MemoryAllocator<byte>? allocator, CancellationToken token)
            where TTransformation : notnull, ITransformation<TResult>
        {
            var buffer = allocator.Invoke(DefaultBufferSize, false);
            try
            {
                return await transformation.TransformAsync(new AsyncStreamBinaryAccessor(input, buffer.Memory), token).ConfigureAwait(false);
            }
            finally
            {
                buffer.Dispose();
                ResetStream(input, resetStream);
            }
        }

        /// <summary>
        /// Decodes the stream.
        /// </summary>
        /// <param name="input">The stream to decode.</param>
        /// <param name="transformation">The decoder.</param>
        /// <param name="resetStream"><see langword="true"/> to reset stream position after decoding.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <typeparam name="TResult">The type of result.</typeparam>
        /// <typeparam name="TTransformation">The type of parser.</typeparam>
        /// <returns>The decoded stream.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        protected static ValueTask<TResult> TransformAsync<TResult, TTransformation>(Stream input, TTransformation transformation, bool resetStream, CancellationToken token)
            where TTransformation : notnull, ITransformation<TResult>
            => TransformAsync<TResult, TTransformation>(input, transformation, resetStream, default(MemoryAllocator<byte>), token);

        /// <summary>
        /// Decodes the data using pipe reader.
        /// </summary>
        /// <param name="input">The pipe reader used for decoding.</param>
        /// <param name="transformation">The decoder.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <typeparam name="TResult">The type of result.</typeparam>
        /// <typeparam name="TTransformation">The type of parser.</typeparam>
        /// <returns>The decoded stream.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        protected static ValueTask<TResult> TransformAsync<TResult, TTransformation>(PipeReader input, TTransformation transformation, CancellationToken token)
            where TTransformation : notnull, ITransformation<TResult>
            => transformation.TransformAsync(new Pipelines.PipeBinaryReader(input), token);

        // use rented buffer of the small size
        private async ValueTask<TResult> GetSmallObjectDataAsync<TResult, TTransformation>(TTransformation parser, long length, CancellationToken token)
            where TTransformation : notnull, ITransformation<TResult>
        {
            using var writer = length <= int.MaxValue ? new PooledArrayBufferWriter<byte>((int)length) : throw new InsufficientMemoryException();

            await WriteToAsync(new AsyncBufferWriter(writer), token).ConfigureAwait(false);
            return await parser.TransformAsync(new SequenceReader(writer.WrittenMemory), token).ConfigureAwait(false);
        }

        // use FileBufferingWriter to keep the balance between I/O performance and memory consumption
        // when size is unknown
        private async ValueTask<TResult> GetUnknownObjectDataAsync<TResult, TTransformation>(TTransformation parser, CancellationToken token)
            where TTransformation : notnull, ITransformation<TResult>
        {
            var output = new FileBufferingWriter(asyncIO: true);
            await using (output.ConfigureAwait(false))
            using (var buffer = MemoryAllocator.Allocate<byte>(DefaultBufferSize, false))
            {
                // serialize
                await WriteToAsync(new AsyncStreamBinaryAccessor(output, buffer.Memory), token).ConfigureAwait(false);

                // deserialize
                if (output.TryGetWrittenContent(out var memory))
                    return await parser.TransformAsync(new SequenceReader(memory), token).ConfigureAwait(false);

                var input = await output.GetWrittenContentAsStreamAsync(token).ConfigureAwait(false);
                await using (input.ConfigureAwait(false))
                    return await parser.TransformAsync(new AsyncStreamBinaryAccessor(input, buffer.Memory), token).ConfigureAwait(false);
            }
        }

        // use disk I/O for large-size object
        private async ValueTask<TResult> GetLargeObjectDataAsync<TResult, TTransformation>(TTransformation parser, long length, CancellationToken token)
            where TTransformation : notnull, ITransformation<TResult>
        {
            var tempFileName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            const FileOptions tempFileOptions = FileOptions.Asynchronous | FileOptions.DeleteOnClose | FileOptions.SequentialScan;
            var fs = new FileStream(tempFileName, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, DefaultBufferSize, tempFileOptions);
            await using (fs.ConfigureAwait(false))
            {
                fs.SetLength(length);

                using var buffer = MemoryAllocator.Allocate<byte>(DefaultBufferSize, false);

                // serialize
                await WriteToAsync(new AsyncStreamBinaryAccessor(fs, buffer.Memory), token).ConfigureAwait(false);
                await fs.FlushAsync(token).ConfigureAwait(false);

                // deserialize
                fs.Position = 0L;
                return await parser.TransformAsync(new AsyncStreamBinaryAccessor(fs, buffer.Memory), token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Converts data transfer object to another type.
        /// </summary>
        /// <remarks>
        /// The default implementation copies the content into memory
        /// before parsing.
        /// </remarks>
        /// <param name="transformation">The parser instance.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <typeparam name="TResult">The type of result.</typeparam>
        /// <typeparam name="TTransformation">The type of parser.</typeparam>
        /// <returns>The converted DTO content.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        ValueTask<TResult> TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token = default)
            where TTransformation : notnull, ITransformation<TResult>
        {
            if (TryGetMemory(out var memory))
                return transformation.TransformAsync(IAsyncBinaryReader.Create(memory), token);

            if (Length.TryGetValue(out var length))
                return length < FileBufferingWriter.Options.DefaultMemoryThreshold ? GetSmallObjectDataAsync<TResult, TTransformation>(transformation, length, token) : GetLargeObjectDataAsync<TResult, TTransformation>(transformation, length, token);

            return GetUnknownObjectDataAsync<TResult, TTransformation>(transformation, token);
        }

        /// <summary>
        /// Attempts to retrieve contents of this object as a memory block synchronously.
        /// </summary>
        /// <param name="memory">The memory block containing contents of this object.</param>
        /// <returns><see langword="true"/> if this object is representable as a memory block; otherwise, <see langword="false"/>.</returns>
        bool TryGetMemory(out ReadOnlyMemory<byte> memory)
        {
            memory = default;
            return false;
        }
    }
}

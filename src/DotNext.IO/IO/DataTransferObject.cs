using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using ByteBuffer = Buffers.ArrayRental<byte>;
    using ByteBufferWriter = Buffers.PooledArrayBufferWriter<byte>;
    using PipeBinaryWriter = Pipelines.PipeBinaryWriter;

    /// <summary>
    /// Various extension methods for <see cref="IDataTransferObject"/>.
    /// </summary>
    public static class DataTransferObject
    {
        private const int DefaultBufferSize = 1024;

        [StructLayout(LayoutKind.Auto)]
        private readonly struct TextDecoder : IDataTransferObject.IDecoder<string>
        {
            private readonly Encoding encoding;
            private readonly int? capacity;

            internal TextDecoder(Encoding encoding)
            {
                this.encoding = encoding;
                capacity = null;
            }

            internal TextDecoder(Encoding encoding, int capacity)
            {
                this.encoding = encoding;
                this.capacity = capacity;
            }

            public async ValueTask<string> ReadAsync<TReader>(TReader reader, CancellationToken token)
                where TReader : IAsyncBinaryReader
            {
                using var writer = capacity.HasValue ?
                    new ByteBufferWriter(capacity.GetValueOrDefault()) :
                    new ByteBufferWriter();
                await reader.CopyToAsync(writer, token).ConfigureAwait(false);
                return writer.WrittenCount == 0 ? string.Empty : encoding.GetString(writer.WrittenMemory.Span);
            }
        }

        private sealed class ArrayDecoder : IDataTransferObject.IDecoder<byte[]>
        {
            internal static readonly ArrayDecoder Instance = new ArrayDecoder();

            private ArrayDecoder()
            {
            }

            public async ValueTask<byte[]> ReadAsync<TReader>(TReader reader, CancellationToken token)
                where TReader : IAsyncBinaryReader
            {
                using var ms = new MemoryStream(DefaultBufferSize);
                await reader.CopyToAsync(ms, token).ConfigureAwait(false);
                ms.Seek(0, SeekOrigin.Begin);
                return ms.ToArray();
            }
        }

        [StructLayout(LayoutKind.Auto)]
        private readonly struct ValueDecoder<T> : IDataTransferObject.IDecoder<T>
            where T : unmanaged
        {
            public ValueTask<T> ReadAsync<TReader>(TReader reader, CancellationToken token)
                where TReader : IAsyncBinaryReader
                => reader.ReadAsync<T>(token);
        }

        [StructLayout(LayoutKind.Auto)]
        private readonly struct DelegatingDecoder<T> : IDataTransferObject.IDecoder<T>
        {
            private readonly Func<IAsyncBinaryReader, CancellationToken, ValueTask<T>> decoder;

            internal DelegatingDecoder(Func<IAsyncBinaryReader, CancellationToken, ValueTask<T>> decoder)
                => this.decoder = decoder;

            ValueTask<T> IDataTransferObject.IDecoder<T>.ReadAsync<TReader>(TReader reader, CancellationToken token)
                => decoder(reader, token);
        }

        /// <summary>
        /// Copies the object content into the specified stream.
        /// </summary>
        /// <typeparam name="TObject">The type of data transfer object.</typeparam>
        /// <param name="dto">Transfer data object to transform.</param>
        /// <param name="output">The output stream receiving object content.</param>
        /// <param name="buffer">The buffer to be used for transformation.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask WriteToAsync<TObject>(this TObject dto, Stream output, Memory<byte> buffer, CancellationToken token = default)
            where TObject : notnull, IDataTransferObject
            => dto.WriteToAsync(new AsyncStreamBinaryWriter(output, buffer), token);

        /// <summary>
        /// Copies the object content to the specified stream.
        /// </summary>
        /// <typeparam name="TObject">The type of data transfer object.</typeparam>
        /// <param name="dto">Transfer data object to transform.</param>
        /// <param name="output">The output stream receiving object content.</param>
        /// <param name="bufferSize">The size of the buffer to be used for transformation.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async ValueTask WriteToAsync<TObject>(this TObject dto, Stream output, int bufferSize = DefaultBufferSize, CancellationToken token = default)
            where TObject : notnull, IDataTransferObject
        {
            using var buffer = new ByteBuffer(bufferSize);
            await WriteToAsync(dto, output, buffer.Memory, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Copies the object content to the specified pipe writer.
        /// </summary>
        /// <typeparam name="TObject">The type of data transfer object.</typeparam>
        /// <param name="dto">Transfer data object to transform.</param>
        /// <param name="output">The pipe writer receiving object content.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask WriteToAsync<TObject>(this TObject dto, PipeWriter output, CancellationToken token = default)
            where TObject : notnull, IDataTransferObject
            => dto.WriteToAsync(new PipeBinaryWriter(output), token);

        /// <summary>
        /// Copies the object content to the specified buffer.
        /// </summary>
        /// <typeparam name="TObject">The type of data transfer object.</typeparam>
        /// <param name="dto">Transfer data object to transform.</param>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask WriteToAsync<TObject>(this TObject dto, IBufferWriter<byte> writer, CancellationToken token = default)
            where TObject : notnull, IDataTransferObject
            => dto.WriteToAsync(new AsyncBufferWriter(writer), token);

        /// <summary>
        /// Converts DTO content to string.
        /// </summary>
        /// <typeparam name="TObject">The type of data transfer object.</typeparam>
        /// <param name="dto">Data transfer object to read from.</param>
        /// <param name="encoding">The encoding used to decode stored string.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The content of the object.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static Task<string> ToStringAsync<TObject>(this TObject dto, Encoding encoding, CancellationToken token = default)
            where TObject : notnull, IDataTransferObject
            => dto.GetObjectDataAsync<string, TextDecoder>(new TextDecoder(encoding), token).AsTask();

        /// <summary>
        /// Converts DTO content to string.
        /// </summary>
        /// <typeparam name="TObject">The type of data transfer object.</typeparam>
        /// <param name="dto">Data transfer object to read from.</param>
        /// <param name="encoding">The encoding used to decode stored string.</param>
        /// <param name="capacity">The maximum possible size of the message, in bytes.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The content of the object.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static Task<string> ToStringAsync<TObject>(this TObject dto, Encoding encoding, int capacity, CancellationToken token = default)
            where TObject : notnull, IDataTransferObject
            => dto.GetObjectDataAsync<string, TextDecoder>(new TextDecoder(encoding, capacity), token).AsTask();

        /// <summary>
        /// Converts DTO to array of bytes.
        /// </summary>
        /// <typeparam name="TObject">The type of data transfer object.</typeparam>
        /// <param name="dto">Data transfer object to read from.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The content of the object.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static Task<byte[]> ToByteArrayAsync<TObject>(this TObject dto, CancellationToken token = default)
            where TObject : notnull, IDataTransferObject
            => dto.GetObjectDataAsync<byte[], ArrayDecoder>(ArrayDecoder.Instance, token).AsTask();

        /// <summary>
        /// Converts DTO to value of blittable type.
        /// </summary>
        /// <typeparam name="TResult">The type of result.</typeparam>
        /// <typeparam name="TObject">The type of data transfer object.</typeparam>
        /// <param name="dto">Data transfer object to read from.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The content of the object.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<TResult> ToType<TResult, TObject>(this TObject dto, CancellationToken token = default)
            where TObject : notnull, IDataTransferObject
            where TResult : unmanaged
            => dto.GetObjectDataAsync<TResult, ValueDecoder<TResult>>(new ValueDecoder<TResult>(), token);

        /// <summary>
        /// Gets the data encapsulated by the data transfer object.
        /// </summary>
        /// <param name="dto">Data transfer object to read from.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <typeparam name="TResult">The type representing another form of data transfer object.</typeparam>
        /// <typeparam name="TObject">The type of the data transfer object.</typeparam>
        /// <returns>The data extracted from DTO.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<TResult> GetObjectDataAsync<TResult, TObject>(this TObject dto, CancellationToken token = default)
            where TResult : notnull, IDataTransferObject.IDecoder<TResult>, new()
            where TObject : notnull, IDataTransferObject
            => dto.GetObjectDataAsync<TResult, TResult>(new TResult(), token);

        /// <summary>
        /// Converts data transfer object to another type.
        /// </summary>
        /// <param name="dto">Data transfer object to decode.</param>
        /// <param name="parser">The parser instance.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <typeparam name="TResult">The type of result.</typeparam>
        /// <typeparam name="TObject">The type of the data transfer object.</typeparam>
        /// <returns>The converted DTO content.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<TResult> GetObjectDataAsync<TResult, TObject>(this TObject dto, Func<IAsyncBinaryReader, CancellationToken, ValueTask<TResult>> parser, CancellationToken token = default)
            where TObject : notnull, IDataTransferObject
            => dto.GetObjectDataAsync<TResult, DelegatingDecoder<TResult>>(new DelegatingDecoder<TResult>(parser), token);
    }
}

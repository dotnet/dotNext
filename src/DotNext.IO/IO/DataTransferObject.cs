using System;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using ByteBuffer = Buffers.ArrayRental<byte>;
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

            private static string ReadAsString(MemoryStream content, Encoding encoding)
            {
                if (content.Length == 0L)
                    return string.Empty;
                if (!content.TryGetBuffer(out var buffer))
                    buffer = new ArraySegment<byte>(content.ToArray());
                return encoding.GetString(buffer.AsSpan());
            }

            public async ValueTask<string> TransformAsync<TReader>(TReader reader, CancellationToken token)
                where TReader : IAsyncBinaryReader
            {
                using var ms = capacity.HasValue ?
                    new RentedMemoryStream(capacity.Value) :
                    new MemoryStream(DefaultBufferSize);
                await reader.CopyToAsync(ms, token).ConfigureAwait(false);
                ms.Seek(0, SeekOrigin.Begin);
                return ReadAsString(ms, encoding);
            }
        }

        [StructLayout(LayoutKind.Auto)]
        private readonly struct ArrayDecoder : IDataTransferObject.IDecoder<byte[]>
        {
            public async ValueTask<byte[]> TransformAsync<TReader>(TReader reader, CancellationToken token)
                where TReader : IAsyncBinaryReader
            {
                using var ms = new MemoryStream(DefaultBufferSize);
                await reader.CopyToAsync(ms, token).ConfigureAwait(false);
                ms.Seek(0, SeekOrigin.Begin);
                return ms.ToArray();
            }
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
        /// Copies the object content into the specified stream.
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
        /// Copies the object content into the specified pipe writer.
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
        /// Converts DTO content into string.
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
        /// Converts DTO content into string.
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
        /// Converts DTO into array of bytes.
        /// </summary>
        /// <typeparam name="TObject">The type of data transfer object.</typeparam>
        /// <param name="dto">Data transfer object to read from.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The content of the object.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static Task<byte[]> ToByteArrayAsync<TObject>(this TObject dto, CancellationToken token = default)
            where TObject : notnull, IDataTransferObject
            => dto.GetObjectDataAsync<byte[], ArrayDecoder>(new ArrayDecoder(), token).AsTask();
    }
}

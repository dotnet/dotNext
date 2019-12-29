using System;
using System.IO;
using System.IO.Pipelines;
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

        /// <summary>
        /// Copies the object content into the specified stream.
        /// </summary>
        /// <param name="dto">Transfer data object to transform.</param>
        /// <param name="output">The output stream receiving object content.</param>
        /// <param name="buffer">The buffer to be used for transformation.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask TransformAsync(this IDataTransferObject dto, Stream output, Memory<byte> buffer, CancellationToken token = default)
            => dto.TransformAsync(new AsyncStreamBinaryWriter(output, buffer), token);

        /// <summary>
        /// Copies the object content into the specified stream.
        /// </summary>
        /// <param name="dto">Transfer data object to transform.</param>
        /// <param name="output">The output stream receiving object content.</param>
        /// <param name="bufferSize">The size of the buffer to be used for transformation.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async ValueTask TransformAsync(this IDataTransferObject dto, Stream output, int bufferSize = DefaultBufferSize, CancellationToken token = default)
        {
            using var buffer = new ByteBuffer(bufferSize);
            await TransformAsync(dto, output, buffer.Memory, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Copies the object content into the specified pipe writer.
        /// </summary>
        /// <param name="dto">Transfer data object to transform.</param>
        /// <param name="output">The pipe writer receiving object content.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask TransformAsync(this IDataTransferObject dto, PipeWriter output, CancellationToken token = default)
            => dto.TransformAsync(new PipeBinaryWriter(output), token);

        private static string ReadAsString(this MemoryStream content, Encoding encoding)
        {
            if (content.Length == 0L)
                return string.Empty;
            if (!content.TryGetBuffer(out var buffer))
                buffer = new ArraySegment<byte>(content.ToArray());
            return encoding.GetString(buffer.AsSpan());
        }

        /// <summary>
        /// Converts DTO content into string.
        /// </summary>
        /// <param name="content">The content to read.</param>
        /// <param name="encoding">The encoding used to decode stored string.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The content of the object.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async Task<string> ReadAsTextAsync(this IDataTransferObject content, Encoding encoding, CancellationToken token = default)
        {
            using var ms = new MemoryStream(DefaultBufferSize);
            await content.TransformAsync(ms, DefaultBufferSize, token).ConfigureAwait(false);
            ms.Seek(0, SeekOrigin.Begin);
            return ms.ReadAsString(encoding);
        }

        /// <summary>
        /// Converts DTO content into string.
        /// </summary>
        /// <param name="content">The content to read.</param>
        /// <param name="encoding">The encoding used to decode stored string.</param>
        /// <param name="capacity">The maximum possible size of the message, in bytes.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The content of the object.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async Task<string> ReadAsTextAsync(this IDataTransferObject content, Encoding encoding, int capacity, CancellationToken token = default)
        {
            using var ms = new RentedMemoryStream(capacity);
            await content.TransformAsync(ms, DefaultBufferSize, token).ConfigureAwait(false);
            ms.Seek(0, SeekOrigin.Begin);
            return ms.ReadAsString(encoding);
        }
    }
}

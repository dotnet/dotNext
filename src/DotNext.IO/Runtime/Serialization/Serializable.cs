using System.IO.Pipelines;

namespace DotNext.Runtime.Serialization;

using Buffers;
using AsyncStreamBinaryAccessor = IO.AsyncStreamBinaryAccessor;
using PipeBinaryReader = IO.Pipelines.PipeBinaryReader;

/// <summary>
/// Extends <see cref="ISerializable{TSelf}"/> implementing types.
/// </summary>
public static class Serializable
{
    /// <summary>
    /// Extends <see cref="ISerializable{TSelf}"/> with static methods.
    /// </summary>
    /// <typeparam name="T">The implementing type.</typeparam>
    extension<T>(T) where T : ISerializable<T>
    {
        /// <summary>
        /// Deserializes the object from the stream.
        /// </summary>
        /// <param name="input">The stream containing serialized data.</param>
        /// <param name="buffer">The buffer to be used for reading from the stream.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>Deserialized object.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<T> ReadFromAsync(Stream input, Memory<byte> buffer, CancellationToken token = default)
            => T.ReadFromAsync<AsyncStreamBinaryAccessor>(new(input, buffer), token);

        /// <summary>
        /// Deserializes the object from the stream.
        /// </summary>
        /// <param name="input">The stream containing serialized data.</param>
        /// <param name="bufferSize">The size of the buffer to be used for reading from the stream.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>Deserialized object.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async ValueTask<T> ReadFromAsync(Stream input, int bufferSize = 128, CancellationToken token = default)
        {
            using var owner = MemoryAllocator<byte>.Default.AllocateAtLeast(bufferSize);
            return await ReadFromAsync<T>(input, owner.Memory, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Deserializes the object from the pipe.
        /// </summary>
        /// <param name="reader">The pipe reader.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>Deserialized object.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<T> ReadFromAsync(PipeReader reader, CancellationToken token = default)
            => T.ReadFromAsync<PipeBinaryReader>(new(reader), token);
    }
}
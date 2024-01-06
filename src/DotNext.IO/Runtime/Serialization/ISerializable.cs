using System.IO.Pipelines;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.Serialization;

using Buffers;
using IO;
using AsyncStreamBinaryAccessor = IO.AsyncStreamBinaryAccessor;
using IDataTransferObject = IO.IDataTransferObject;
using PipeBinaryReader = IO.Pipelines.PipeBinaryReader;

/// <summary>
/// Represents an object that supports serialization and deserialization.
/// </summary>
/// <typeparam name="TSelf">The implementing type.</typeparam>
public interface ISerializable<TSelf> : IDataTransferObject
    where TSelf : ISerializable<TSelf>
{
    /// <inheritdoc />
    bool IDataTransferObject.IsReusable => true;

    /// <summary>
    /// Decodes the object of type <typeparamref name="TSelf"/> from its binary representation.
    /// </summary>
    /// <typeparam name="TReader">The type of the reader.</typeparam>
    /// <param name="reader">The reader.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The decoded object.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static abstract ValueTask<TSelf> ReadFromAsync<TReader>(TReader reader, CancellationToken token)
        where TReader : notnull, IAsyncBinaryReader;

    /// <summary>
    /// Deserializes the object from the stream.
    /// </summary>
    /// <param name="input">The stream containing serialized data.</param>
    /// <param name="buffer">The buffer to be used for reading from the stream.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>Deserialized object.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<TSelf> ReadFromAsync(Stream input, Memory<byte> buffer, CancellationToken token = default)
        => TSelf.ReadFromAsync<AsyncStreamBinaryAccessor>(new(input, buffer), token);

    /// <summary>
    /// Deserializes the object from the stream.
    /// </summary>
    /// <param name="input">The stream containing serialized data.</param>
    /// <param name="bufferSize">The size of the buffer to be used for reading from the stream.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>Deserialized object.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask<TSelf> ReadFromAsync(Stream input, int bufferSize = 128, CancellationToken token = default)
    {
        using var owner = Memory.AllocateAtLeast<byte>(bufferSize);
        return await ReadFromAsync(input, owner.Memory, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Deserializes the object from the pipe.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>Deserialized object.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<TSelf> ReadFromAsync(PipeReader reader, CancellationToken token = default)
        => TSelf.ReadFromAsync<PipeBinaryReader>(new(reader), token);

    /// <summary>
    /// Transforms one object into another object.
    /// </summary>
    /// <typeparam name="TInput">The type of the object to transform.</typeparam>
    /// <param name="input">The object to transform.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>Deserialized object.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<TSelf> TransformAsync<TInput>(TInput input, CancellationToken token = default)
        where TInput : notnull, IDataTransferObject
        => input.TransformAsync<TSelf, DeserializingTransformation>(new(), token);

    [StructLayout(LayoutKind.Auto)]
    private readonly struct DeserializingTransformation : ITransformation<TSelf>
    {
        ValueTask<TSelf> ITransformation<TSelf>.TransformAsync<TReader>(TReader reader, CancellationToken token)
            => TSelf.ReadFromAsync(reader, token);
    }
}
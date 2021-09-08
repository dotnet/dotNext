using System.IO.Pipelines;

namespace DotNext.Runtime.Serialization;

using Buffers;
using AsyncStreamBinaryAccessor = IO.AsyncStreamBinaryAccessor;
using PipeBinaryReader = IO.Pipelines.PipeBinaryReader;

/// <summary>
/// Provides extension methods for decoding <see cref="ISerializable{TSelf}"/>
/// from various sources.
/// </summary>
public static class Serializable
{
    /// <summary>
    /// Deserializes the object from the stream.
    /// </summary>
    /// <typeparam name="TObject">The type of the object to deserialize.</typeparam>
    /// <param name="input">The stream containing serialized data.</param>
    /// <param name="buffer">The buffer to be used for reading from the stream.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>Deserialized object.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<TObject> ReadFromAsync<TObject>(this Stream input, Memory<byte> buffer, CancellationToken token = default)
        where TObject : notnull, ISerializable<TObject>
        => TObject.ReadFromAsync<AsyncStreamBinaryAccessor>(new(input, buffer), token);

    /// <summary>
    /// Deserializes the object from the stream.
    /// </summary>
    /// <typeparam name="TObject">The type of the object to deserialize.</typeparam>
    /// <param name="input">The stream containing serialized data.</param>
    /// <param name="bufferSize">The size of the buffer to be used for reading from the stream.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>Deserialized object.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask<TObject> ReadFromAsync<TObject>(this Stream input, int bufferSize = 128, CancellationToken token = default)
        where TObject : notnull, ISerializable<TObject>
    {
        using var owner = MemoryAllocator.Allocate<byte>(bufferSize, false);
        return await ReadFromAsync<TObject>(input, owner.Memory, token);
    }

    /// <summary>
    /// Deserializes the object from the pipe.
    /// </summary>
    /// <typeparam name="TObject">The type of the object to deserialize.</typeparam>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>Deserialized object.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<TObject> ReadFromAsync<TObject>(this PipeReader reader, CancellationToken token = default)
        where TObject : notnull, ISerializable<TObject>
        => TObject.ReadFromAsync<PipeBinaryReader>(new(reader), token);
}
namespace DotNext.Runtime.Serialization;

using IO;
using Patterns;

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
        where TReader : IAsyncBinaryReader;

    /// <summary>
    /// Transforms one object into another object.
    /// </summary>
    /// <typeparam name="TInput">The type of the object to transform.</typeparam>
    /// <param name="input">The object to transform.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>Deserialized object.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<TSelf> TransformAsync<TInput>(TInput input, CancellationToken token = default)
        where TInput : IDataTransferObject
        => input.TransformAsync<TSelf, DeserializingTransformation>(DeserializingTransformation.Instance, token);
    
    private sealed class DeserializingTransformation : ITransformation<TSelf>, ISingleton<DeserializingTransformation>
    {
        public static DeserializingTransformation Instance { get; } = new();

        private DeserializingTransformation()
        {
        }
        
        ValueTask<TSelf> ITransformation<TSelf>.TransformAsync<TReader>(TReader reader, CancellationToken token)
            => TSelf.ReadFromAsync(reader, token);
    }
}
using System.Runtime.Versioning;

namespace DotNext.Runtime.Serialization;

using IO;

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
    [RequiresPreviewFeatures]
    public static abstract ValueTask<TSelf> ReadFromAsync<TReader>(TReader reader, CancellationToken token)
        where TReader : notnull, IAsyncBinaryReader;
}
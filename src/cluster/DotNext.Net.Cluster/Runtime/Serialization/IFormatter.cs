using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Runtime.Serialization
{
    using IO;

    /// <summary>
    /// Represents serialization and deserialization logic for the specific type.
    /// </summary>
    /// <typeparam name="T">The type to be serialized.</typeparam>
    public interface IFormatter<T>
        where T : struct
    {
        /// <summary>
        /// Serializes the value asynchronously.
        /// </summary>
        /// <param name="obj">The object to be serialized.</param>
        /// <param name="writer">The writer that can be used for encoding object data.</param>
        /// <param name="token">The token that can be used to cancel the serialization.</param>
        /// <typeparam name="TWriter">The type of the binary writer.</typeparam>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        ValueTask SerializeAsync<TWriter>(T obj, TWriter writer, CancellationToken token)
            where TWriter : notnull, IAsyncBinaryWriter;

        /// <summary>
        /// Deserializes the value asynchronously.
        /// </summary>
        /// <param name="reader">The reader of the encoded object data.</param>
        /// <param name="token">The token that can be used to cancel the deserialization.</param>
        /// <typeparam name="TReader">The type of the binary reader.</typeparam>
        /// <returns>The deserialized object.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        ValueTask<T> DeserializeAsync<TReader>(TReader reader, CancellationToken token)
            where TReader : notnull, IAsyncBinaryReader;

        /// <summary>
        /// Gets length of the object data, in bytes.
        /// </summary>
        /// <param name="command">The command data.</param>
        /// <returns>The length of the object data, in bytes; or <see langword="null"/> if the length cannot be determined.</returns>
        long? GetLength(in T command) => null;
    }
}
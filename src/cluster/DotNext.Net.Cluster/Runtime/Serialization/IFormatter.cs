using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Commands
{
    using IO;

    /// <summary>
    /// Represents serialization logic for the command.
    /// </summary>
    /// <typeparam name="TCommand">The type of the command.</typeparam>
    public interface ICommandFormatter<TCommand>
        where TCommand : struct
    {
        /// <summary>
        /// Serializes command data asynchronously.
        /// </summary>
        /// <param name="command">Command data to be serialized.</param>
        /// <param name="writer">The writer that can be used for encoding command data.</param>
        /// <param name="token">The token that can be used to cancel the serialization.</param>
        /// <typeparam name="TWriter">The type of the binary writer.</typeparam>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        ValueTask SerializeAsync<TWriter>(TCommand command, TWriter writer, CancellationToken token)
            where TWriter : notnull, IAsyncBinaryWriter;

        /// <summary>
        /// Deserializes command data asynchronously.
        /// </summary>
        /// <param name="reader">The reader of the encoded command data.</param>
        /// <param name="token">The token that can be used to cancel the deserialization.</param>
        /// <typeparam name="TReader">The type of the binary reader.</typeparam>
        /// <returns>The deserialized command data.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        ValueTask<TCommand> DeserializeAsync<TReader>(TReader reader, CancellationToken token)
            where TReader : notnull, IAsyncBinaryReader;

        /// <summary>
        /// Gets length of the command data, in bytes.
        /// </summary>
        /// <param name="command">The command data.</param>
        /// <returns>The length of the command data, in bytes; or <see langword="null"/> if the length cannot be determined.</returns>
        long? GetLength(in TCommand command) => null;
    }
}
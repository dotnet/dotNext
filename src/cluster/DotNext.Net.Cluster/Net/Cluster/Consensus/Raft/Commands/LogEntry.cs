using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.Commands;

using IO;
using Runtime.Serialization;

/// <summary>
/// Represents Raft log entry containing custom command.
/// </summary>
/// <typeparam name="TCommand">The type of the command encoded by the log entry.</typeparam>
/// <seealso cref="Text.Json.JsonSerializable{T}"/>
[StructLayout(LayoutKind.Auto)]
public readonly struct LogEntry<TCommand>() : IRaftLogEntry
    where TCommand : ISerializable<TCommand>
{
    /// <summary>
    /// Gets the command associated with this log entry.
    /// </summary>
    required public TCommand Command { get; init; }

    /// <inheritdoc />
    required public long Term { get; init; }

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets identifier of the underlying command associated with this log entry.
    /// </summary>
    required public int CommandId { get; init; }

    /// <inheritdoc />
    int? IRaftLogEntry.CommandId => CommandId;

    /// <inheritdoc />
    bool IDataTransferObject.IsReusable => true;

    /// <inheritdoc />
    long? IDataTransferObject.Length => Command.Length;

    /// <inheritdoc />
    ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        => Command.WriteToAsync(writer, token);
}
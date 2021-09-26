using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.Commands;

using IO;
using Runtime.Serialization;

/// <summary>
/// Represents Raft log entry containing custom command.
/// </summary>
/// <typeparam name="TCommand">The type of the command encoded by the log entry.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly struct LogEntry<TCommand> : IRaftLogEntry
    where TCommand : notnull, ISerializable<TCommand>
{
    internal LogEntry(long term, TCommand command, int id)
    {
        Term = term;
        Timestamp = DateTimeOffset.UtcNow;
        Command = command;
        CommandId = id;
    }

    /// <summary>
    /// Gets the command associated with this log entry.
    /// </summary>
    public TCommand Command { get; }

    /// <inheritdoc />
    public long Term { get; }

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets identifier of the underlying command associated with this log entry.
    /// </summary>
    public int CommandId { get; }

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
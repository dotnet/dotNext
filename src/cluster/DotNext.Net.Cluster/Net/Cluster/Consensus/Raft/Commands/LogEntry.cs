using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.Commands;

using IO;
using IO.Log;

/// <summary>
/// Represents Raft log entry containing custom command.
/// </summary>
/// <typeparam name="TCommand">The type of the command encoded by the log entry.</typeparam>
/// <seealso cref="Text.Json.JsonSerializable{T}"/>
[StructLayout(LayoutKind.Auto)]
public readonly struct LogEntry<TCommand>() : IRaftLogEntry
    where TCommand : ICommand<TCommand>
{
    /// <summary>
    /// Gets the command associated with this log entry.
    /// </summary>
    public required TCommand Command { get; init; }

    /// <inheritdoc />
    public required long Term { get; init; }

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    int? IRaftLogEntry.CommandId => TCommand.Id;

    /// <inheritdoc />
    bool IDataTransferObject.IsReusable => true;

    /// <inheritdoc />
    long? IDataTransferObject.Length => Command.Length;

    /// <inheritdoc />
    ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        => Command.WriteToAsync(writer, token);

    /// <inheritdoc />
    bool ILogEntry.IsSnapshot => TCommand.IsSnapshot;
}
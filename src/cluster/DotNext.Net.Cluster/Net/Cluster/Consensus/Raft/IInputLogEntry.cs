namespace DotNext.Net.Cluster.Consensus.Raft;

/// <summary>
/// Represents a custom log entry that can be passed to the log.
/// </summary>
public interface IInputLogEntry : IRaftLogEntry
{
    /// <summary>
    /// Gets or sets runtime context associated with the log entry.
    /// </summary>
    /// <remarks>
    /// The value passes through <see cref="IO.Log.IAuditTrail{TEntry}.AppendAsync{TEntryImpl}(TEntryImpl, CancellationToken)"/>
    /// to <see cref="MemoryBasedStateMachine.ApplyAsync(PersistentState.LogEntry)"/> or <see cref="DiskBasedStateMachine.ApplyAsync(PersistentState.LogEntry)"/>.
    /// It can be retrieved by using <see cref="PersistentState.LogEntry.Context"/> property.
    /// </remarks>
    object? Context { get; init; }
}
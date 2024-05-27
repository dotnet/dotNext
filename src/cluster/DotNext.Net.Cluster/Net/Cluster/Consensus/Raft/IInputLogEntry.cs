namespace DotNext.Net.Cluster.Consensus.Raft;

/// <summary>
/// Represents a custom log entry that can be passed to the log.
/// </summary>
public interface IInputLogEntry : IRaftLogEntry
{
    /// <summary>
    /// Gets or sets runtime context associated with the log entry.
    /// </summary>
    object? Context { get; init; }
}
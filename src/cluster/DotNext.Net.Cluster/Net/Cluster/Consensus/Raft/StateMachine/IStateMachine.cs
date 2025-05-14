using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

/// <summary>
/// Represents state machine.
/// </summary>
[Experimental("DOTNEXT001")]
public interface IStateMachine : ISnapshotManager
{
    /// <summary>
    /// Applies the log entry to the underlying state machine.
    /// </summary>
    /// <param name="entry">The log entry to be applied.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The index of the last applied log entry. It should be greater than or equal to <see cref="LogEntry.Index"/>.</returns>
    /// <exc
    ValueTask<long> ApplyAsync(LogEntry entry, CancellationToken token);
}
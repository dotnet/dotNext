using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using IO.Log;

/// <summary>
/// Represents persistent log entry maintained by the <see cref="WriteAheadLog"/>.
/// </summary>
/// <remarks>
/// This interface is not supposed to be implemented by the user.
/// </remarks>
[Experimental("DOTNEXT001")]
public interface ISnapshot : IRaftLogEntry
{
    /// <summary>
    /// Gets the index of the log entry.
    /// </summary>
    long Index { get; }

    /// <inheritdoc/>
    bool ILogEntry.IsSnapshot => true;
}
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

/// <summary>
/// Gets the replication status of the member.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public record struct ReplicationStatus
{
    /// <summary>
    /// Gets the heartbeat result.
    /// </summary>
    public required HeartbeatResult Result { get; init; }
    
    /// <summary>
    /// Gets the index of the last log entry.
    /// </summary>
    public required long LastIndex { get; init; }
}
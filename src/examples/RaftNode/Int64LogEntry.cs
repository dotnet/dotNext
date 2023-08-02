using DotNext.IO;
using DotNext.IO.Log;
using DotNext.Net.Cluster.Consensus.Raft;

namespace RaftNode;

internal sealed class Int64LogEntry : BinaryTransferObject<long>, IRaftLogEntry
{
    bool ILogEntry.IsSnapshot => false;

    public long Term { get; init; }

    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
}
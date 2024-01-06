using DotNext.IO;
using DotNext.IO.Log;
using DotNext.Net.Cluster.Consensus.Raft;

namespace RaftNode;

internal readonly struct Int64LogEntry() : IRaftLogEntry
{
    required public long Value { get; init; }

    bool ILogEntry.IsSnapshot => false;

    required public long Term { get; init; }

    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

    bool IDataTransferObject.IsReusable => true;

    long? IDataTransferObject.Length => null;

    ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        => writer.WriteLittleEndianAsync(Value, token);
}
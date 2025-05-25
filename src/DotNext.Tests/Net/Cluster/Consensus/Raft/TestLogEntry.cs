using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft;

using ILogEntry = IO.Log.ILogEntry;
using TextMessage = Messaging.TextMessage;

[ExcludeFromCodeCoverage]
internal sealed class TestLogEntry(string command) : TextMessage(command, "Entry"), IInputLogEntry
{
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

    public long Term { get; init; }

    bool ILogEntry.IsSnapshot => false;

    public object Context
    {
        get;
        init;
    }
}
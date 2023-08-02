using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft;

using ILogEntry = IO.Log.ILogEntry;
using TextMessage = Messaging.TextMessage;

[ExcludeFromCodeCoverage]
internal sealed class TestLogEntry : TextMessage, IRaftLogEntry
{
    public TestLogEntry(string command)
        : base(command, "Entry")
    {
        Timestamp = DateTimeOffset.UtcNow;
    }

    public DateTimeOffset Timestamp { get; }

    public long Term { get; set; }

    bool ILogEntry.IsSnapshot => false;
}
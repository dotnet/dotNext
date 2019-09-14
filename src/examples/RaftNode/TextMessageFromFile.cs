using System;
using DotNext.Net.Cluster.Consensus.Raft;
using DotNext.Net.Cluster.Messaging;

namespace RaftNode
{
    internal sealed class TextMessageFromFile : TextMessage, IRaftLogEntry
    {
        internal new const string Type = "TextMessage";

        internal TextMessageFromFile(string content)
            : base(content, Type)
        {
            Timestamp = DateTimeOffset.UtcNow;
        }

        bool DotNext.Net.Cluster.Replication.ILogEntry.IsSnapshot => false;

        public long Term { get; set; }

        public DateTimeOffset Timestamp { get; }
    }
}

using DotNext.Net.Cluster.Consensus.Raft;
using DotNext.Net.Cluster.Messaging;
using System;

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

        public long Term { get; set; }

        public DateTimeOffset Timestamp { get; }
    }
}

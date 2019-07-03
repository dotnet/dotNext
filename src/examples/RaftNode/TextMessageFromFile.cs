using DotNext.Net.Cluster.Consensus.Raft;
using DotNext.Net.Cluster.Messaging;

namespace RaftNode
{
    internal sealed class TextMessageFromFile : TextMessage, ILogEntry
    {
        internal new const string Type = "TextMessage";

        internal TextMessageFromFile(string content)
            : base(content, Type)
        {

        }

        public long Term { get; set; }
    }
}

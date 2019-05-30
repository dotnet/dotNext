using System;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal sealed class AppendEntriesMessage : RaftHttpMessage
    {
        internal const string MessageType = "AppendEntries";

        internal AppendEntriesMessage(Guid memberId, long consensusTerm)
            : base(MessageType, memberId, consensusTerm)
        {
        }

        internal IMessage CustomPayload { private get; set; }
    }
}
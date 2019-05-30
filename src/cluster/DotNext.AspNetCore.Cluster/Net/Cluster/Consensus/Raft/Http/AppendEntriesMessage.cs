using System;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class AppendEntriesMessage : RaftHttpMessage
    {
        internal const string MessageType = "AppendEntries";

        internal AppendEntriesMessage(IRaftLocalMember sender)
            : base(MessageType, sender)
        {
        }

        internal IMessage CustomPayload { private get; set; }
    }
}
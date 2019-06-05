using Microsoft.AspNetCore.Http;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class ResignMessage : RaftHttpBooleanMessage
    {
        internal const string MessageType = "Resign";

        internal ResignMessage(ISite owner)
            : base(MessageType, owner)
        {
        }

        internal ResignMessage(HttpRequest request)
            : base(request)
        {
        }
    }
}

using Microsoft.AspNetCore.Http;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class ResignMessage : RaftHttpBooleanMessage
    {
        internal const string MessageType = "Resign";

        internal ResignMessage(ILocalClusterMember sender)
            : base(MessageType, sender)
        {
        }

        internal ResignMessage(HttpRequest request)
            : base(request)
        {
        }
    }
}

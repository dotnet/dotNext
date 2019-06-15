using System.Net;
using Microsoft.AspNetCore.Http;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class ResignMessage : RaftHttpBooleanMessage
    {
        internal new const string MessageType = "Resign";

        internal ResignMessage(IPEndPoint sender)
            : base(MessageType, sender)
        {
        }

        internal ResignMessage(HttpRequest request)
            : base(request)
        {
        }
    }
}

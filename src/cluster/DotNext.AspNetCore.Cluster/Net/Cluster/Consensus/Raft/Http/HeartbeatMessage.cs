using System.Net;
using Microsoft.AspNetCore.Http;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class HeartbeatMessage : RaftHttpMessage
    {
        internal new const string MessageType = "Heartbeat";

        internal HeartbeatMessage(IPEndPoint sender)
            : base(MessageType, sender)
        {
        }

        internal HeartbeatMessage(HttpRequest request)
            : base(request)
        {
        }

        internal static void CreateResponse(HttpResponse response)
        {
            response.StatusCode = (int) HttpStatusCode.OK;
        }
    }
}

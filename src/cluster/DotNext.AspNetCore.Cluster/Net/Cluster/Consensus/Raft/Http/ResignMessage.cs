using Microsoft.AspNetCore.Http;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class ResignMessage : HttpMessage, IHttpMessageReader<bool>, IHttpMessageWriter<bool>
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

        Task<bool> IHttpMessageReader<bool>.ParseResponse(HttpResponseMessage response) => ParseBoolResponse(response);

        public new Task SaveResponse(HttpResponse response, bool result) => HttpMessage.SaveResponse(response, result);
    }
}

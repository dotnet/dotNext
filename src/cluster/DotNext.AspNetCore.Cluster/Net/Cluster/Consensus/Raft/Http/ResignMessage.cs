using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class ResignMessage : HttpMessage, IHttpMessageReader<bool>, IHttpMessageWriter<bool>
    {
        internal new const string MessageType = "Resign";

        internal ResignMessage(IPEndPoint sender)
            : base(MessageType, sender)
        {
        }

        private ResignMessage(HeadersReader<StringValues> headers)
            : base(headers)
        {
        }

        internal ResignMessage(HttpRequest request)
            : this(request.Headers.TryGetValue)
        {
        }

        Task<bool> IHttpMessageReader<bool>.ParseResponse(HttpResponseMessage response, CancellationToken token) => ParseBoolResponse(response, token);

        public new Task SaveResponse(HttpResponse response, bool result, CancellationToken token) => HttpMessage.SaveResponse(response, result, token);
    }
}

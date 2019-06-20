using Microsoft.AspNetCore.Http;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Messaging;

    internal sealed class CustomMessage : HttpMessage, IHttpMessage<IMessage>
    {
        internal new const string MessageType = "CustomMessage";
        private const string OneWayHeader = "X-OneWay";

        internal readonly bool IsOneWay;
        internal readonly IMessage Message;

        internal CustomMessage(IPEndPoint sender, IMessage message, bool oneWay)
            : base(MessageType, sender)
        {
            Message = message;
            IsOneWay = oneWay;
        }

        internal CustomMessage(HttpRequest request)
            : base(request)
        {
            foreach (var header in request.Headers[OneWayHeader])
                if (bool.TryParse(header, out IsOneWay))
                    break;
            Message = new InboundMessageContent(request);
        }

        private protected override void FillRequest(HttpRequestMessage request)
        {
            base.FillRequest(request);
            request.Headers.Add(OneWayHeader, Convert.ToString(IsOneWay, InvariantCulture));
            request.Content = new OutboundMessageContent(Message);
        }

        Task<IMessage> IHttpMessage<IMessage>.ParseResponse(HttpResponseMessage response)
            => Task.FromResult<IMessage>(new InboundMessageContent(response));

        public Task SaveResponse(HttpResponse response, IMessage message)
        {
            response.StatusCode = StatusCodes.Status200OK;
            return OutboundMessageContent.WriteTo(message, response);
        }
    }
}

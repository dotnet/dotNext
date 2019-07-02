using Microsoft.AspNetCore.Http;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Messaging;
    using NullMessage = Threading.Tasks.CompletedTask<Messaging.IMessage, Generic.DefaultConst<Messaging.IMessage>>;
    using static Threading.Tasks.Conversion;

    internal class CustomMessage : HttpMessage, IHttpMessageWriter<IMessage>, IHttpMessageReader<IMessage>
    {
        internal new const string MessageType = "CustomMessage";
        private const string OneWayHeader = "X-OneWay-Message";

        private const string RespectLeadershipHeader = "X-Respect-Leadership";

        internal readonly bool IsOneWay;
        internal readonly IMessage Message;
        internal bool RespectLeadership;

        internal CustomMessage(IPEndPoint sender, IMessage message, bool oneWay)
            : base(MessageType, sender)
        {
            Message = message;
            IsOneWay = oneWay;
        }

        private CustomMessage(HeadersReader<StringValues> headers)
            : base(headers)
        {
            IsOneWay = ParseHeader(OneWayHeader, headers, BooleanParser);
            RespectLeadership = ParseHeader(RespectLeadershipHeader, headers, BooleanParser);
        }

        internal CustomMessage(HttpRequest request)
            : this(request.Headers.TryGetValue)
        {
            Message = new InboundMessageContent(request);
        }

        private protected sealed override void FillRequest(HttpRequestMessage request)
        {
            base.FillRequest(request);
            request.Headers.Add(OneWayHeader, Convert.ToString(IsOneWay, InvariantCulture));
            request.Headers.Add(RespectLeadershipHeader, Convert.ToString(RespectLeadershipHeader, InvariantCulture));
            request.Content = new OutboundMessageContent(Message);
        }

        public Task SaveResponse(HttpResponse response, IMessage message)
        {
            response.StatusCode = StatusCodes.Status200OK;
            return OutboundMessageContent.WriteTo(message, response);
        }

        Task<IMessage> IHttpMessageReader<IMessage>.ParseResponse(HttpResponseMessage response)
            => response.StatusCode == HttpStatusCode.NoContent ? 
                NullMessage.Task 
                : InboundMessageContent.FromResponseAsync(response, StreamMessage.CreateBufferedMessageAsync).Convert<StreamMessage, IMessage>();
    }

    internal sealed class CustomMessage<T> : CustomMessage, IHttpMessageReader<T>
    {
        private readonly MessageReader<T> reader;

        internal CustomMessage(IPEndPoint sender, IMessage message, MessageReader<T> reader) : base(sender, message, false) => this.reader = reader;

        Task<T> IHttpMessageReader<T>.ParseResponse(HttpResponseMessage response)
            => InboundMessageContent.FromResponseAsync(response, reader);
    }
}

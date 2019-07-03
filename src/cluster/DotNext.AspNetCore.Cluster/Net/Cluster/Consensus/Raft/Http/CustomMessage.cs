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

    internal class CustomMessage : HttpMessage, IHttpMessageWriter<IMessage>, IHttpMessageReader<IMessage>
    {
        private static readonly ValueParser<DeliveryMode> DeliveryModeParser = Enum.TryParse<DeliveryMode>;

        internal enum DeliveryMode
        {
            OneWayNoAck,
            OneWay,
            RequestReply
        }

        internal new const string MessageType = "CustomMessage";
        private const string DeliveryModeHeader = "X-Delivery-Type";

        private const string RespectLeadershipHeader = "X-Respect-Leadership";

        internal readonly DeliveryMode Mode;
        internal readonly IMessage Message;
        internal bool RespectLeadership;

        private protected CustomMessage(IPEndPoint sender, IMessage message, DeliveryMode mode)
            : base(MessageType, sender)
        {
            Message = message;
            Mode = mode;
        }

        internal CustomMessage(IPEndPoint sender, IMessage message, bool requiresConfirmation)
            : this(sender, message, requiresConfirmation ? DeliveryMode.OneWay : DeliveryMode.OneWayNoAck)
        {

        }

        private CustomMessage(HeadersReader<StringValues> headers)
            : base(headers)
        {
            Mode = ParseHeader(DeliveryModeHeader, headers, DeliveryModeParser);
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
            request.Headers.Add(DeliveryModeHeader, Mode.ToString());
            request.Headers.Add(RespectLeadershipHeader, Convert.ToString(RespectLeadership, InvariantCulture));
            request.Content = new OutboundMessageContent(Message);
        }

        public Task SaveResponse(HttpResponse response, IMessage message)
        {
            response.StatusCode = StatusCodes.Status200OK;
            return OutboundMessageContent.WriteTo(message, response);
        }

        //do not parse response because this is one-way message
        Task<IMessage> IHttpMessageReader<IMessage>.ParseResponse(HttpResponseMessage response) => NullMessage.Task;
    }

    internal sealed class CustomMessage<T> : CustomMessage, IHttpMessageReader<T>
    {
        private readonly MessageReader<T> reader;

        internal CustomMessage(IPEndPoint sender, IMessage message, MessageReader<T> reader) : base(sender, message, DeliveryMode.RequestReply) => this.reader = reader;

        Task<T> IHttpMessageReader<T>.ParseResponse(HttpResponseMessage response)
            => InboundMessageContent.FromResponseAsync(response, reader);
    }
}

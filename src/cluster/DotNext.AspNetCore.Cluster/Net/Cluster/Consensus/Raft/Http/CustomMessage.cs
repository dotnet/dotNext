using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mime;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Messaging;
    using NullMessage = Threading.Tasks.CompletedTask<Messaging.IMessage, Generic.DefaultConst<Messaging.IMessage>>;

    internal class CustomMessage : HttpMessage, IHttpMessageWriter<IMessage>, IHttpMessageReader<IMessage>
    {
        //request - represents custom message name
        private const string MessageNameHeader = "X-Raft-Message-Name";
        private static readonly ValueParser<DeliveryMode> DeliveryModeParser = Enum.TryParse;

        internal enum DeliveryMode
        {
            OneWayNoAck,
            OneWay,
            RequestReply
        }

        private sealed class OutboundMessageContent : OutboundTransferObject
        {
            internal OutboundMessageContent(IMessage message)
                : base(message)
            {
                Headers.ContentType = MediaTypeHeaderValue.Parse(message.Type.ToString());
                Headers.Add(MessageNameHeader, message.Name);
            }

            internal static Task WriteTo(IMessage message, HttpResponse response)
            {
                response.ContentType = message.Type.ToString();
                response.ContentLength = message.Length;
                response.Headers.Add(MessageNameHeader, message.Name);
                return message.CopyToAsync(response.Body);
            }
        }

        private protected class InboundMessageContent : StreamMessage
        {
            private InboundMessageContent(Stream content, bool leaveOpen, string name, ContentType type)
                : base(content, leaveOpen, name, type)
            {
            }

            internal InboundMessageContent(HttpRequest request)
                : this(request.Body, true, ParseHeader<StringValues>(MessageNameHeader, request.Headers.TryGetValue),
                    new ContentType(request.ContentType))
            {
            }

            private protected InboundMessageContent(MultipartSection section)
                : this(section.Body, true, ParseHeader<StringValues>(MessageNameHeader, section.Headers.TryGetValue),
                    new ContentType(section.ContentType))
            {

            }

            internal static async Task<TResponse> FromResponseAsync<TResponse>(HttpResponseMessage response,
                MessageReader<TResponse> reader)
            {
                var contentType = new ContentType(response.Content.Headers.ContentType.ToString());
                var name = ParseHeader<IEnumerable<string>>(MessageNameHeader, response.Headers.TryGetValues);
                using (var content = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var message = new InboundMessageContent(content, true, name, contentType))
                    return await reader(message).ConfigureAwait(false);
            }
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
            request.Headers.Add(RespectLeadershipHeader, RespectLeadership.ToString(InvariantCulture));
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

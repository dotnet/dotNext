using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Threading.Tasks;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Messaging;

    internal abstract class HttpMessage
    {
        //request - represents IP of sender node
        private const string NodeIpHeader = "X-Raft-Node-IP";
        //request - represents hosting port of sender node
        private const string NodePortHeader = "X-Raft-Node-Port";
        //request - represents request message type
        private const string MessageTypeHeader = "X-Raft-Message-Type";

        //request - represents custom message name
        private const string MessageNameHeader = "X-Raft-Message-Name";

        private protected class OutboundMessageContent : HttpContent
        {
            private readonly IMessage message;

            internal OutboundMessageContent(IMessage message)
            {
                this.message = message;
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

            protected sealed override Task SerializeToStreamAsync(Stream stream, TransportContext context)
                => message.CopyToAsync(stream);

            protected sealed override bool TryComputeLength(out long length)
                => message.Length.TryGet(out length);
        }

        private protected class InboundMessageContent : StreamMessage
        {
            private InboundMessageContent(Stream content, bool leaveOpen, string name, ContentType type)
                : base(content, leaveOpen, name, type)
            {
            }

            internal InboundMessageContent(HttpRequest request)
                : this(request.Body, true, request.Headers[MessageNameHeader].FirstOrDefault() ?? throw new RaftProtocolException(ExceptionMessages.MissingHeader(MessageNameHeader)), new ContentType(request.ContentType))
            {
            }

            internal static async Task<InboundMessageContent> FromResponseAsync(HttpResponseMessage response)
            {
                var contentType = new ContentType(response.Content.Headers.ContentType.ToString());
                var name = response.Headers.TryGetValues(MessageNameHeader, out var values)
                    ? values.FirstOrDefault()
                    : null;
                if (name is null)
                    throw new RaftProtocolException(ExceptionMessages.MissingHeader(MessageNameHeader));
                var content = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                return new InboundMessageContent(content, false, name, contentType);
            }
        }

        internal readonly IPEndPoint Sender;
        internal readonly string MessageType;

        private protected HttpMessage(string messageType, IPEndPoint sender)
        {
            Sender = sender;
            MessageType = messageType;
        }

        private protected HttpMessage(HttpRequest request)
        {
            var address = default(IPAddress);
            var port = 0;
            foreach (var header in request.Headers[NodeIpHeader])
                if (IPAddress.TryParse(header, out address))
                    break;
            foreach (var header in request.Headers[NodePortHeader])
                if (int.TryParse(header, out port))
                    break;
            Sender = new IPEndPoint(address ?? throw new RaftProtocolException(ExceptionMessages.MissingHeader(NodeIpHeader)), port);
            MessageType = GetMessageType(request);
        }

        internal static string GetMessageType(HttpRequest request)
            => request.Headers.TryGetValue(MessageTypeHeader, out var values) ? values.First() : throw new RaftProtocolException(ExceptionMessages.MissingHeader(MessageTypeHeader));
            
        private protected virtual void FillRequest(HttpRequestMessage request)
        {
            request.Headers.Add(NodeIpHeader, Sender.Address.ToString());
            request.Headers.Add(NodePortHeader, Convert.ToString(Sender.Port, InvariantCulture));
            request.Headers.Add(MessageTypeHeader, MessageType);
            request.Method = HttpMethod.Post;
        }

        public static explicit operator HttpRequestMessage(HttpMessage message)
        {
            if (message is null)
                return null;
            var request = new HttpRequestMessage { Method = HttpMethod.Post };
            message.FillRequest(request);
            return request;
        }

        private protected static async Task<bool> ParseBoolResponse(HttpResponseMessage response)
            => bool.TryParse(await response.Content.ReadAsStringAsync().ConfigureAwait(false), out var result)
                ? result
                : throw new RaftProtocolException(ExceptionMessages.IncorrectResponse);

        private protected static Task SaveResponse(HttpResponse response, bool result)
        {
            response.StatusCode = StatusCodes.Status200OK;
            return response.WriteAsync(Convert.ToString(result, InvariantCulture));
        }
    }
}
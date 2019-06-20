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

    internal abstract class RaftHttpMessage
    {
        //request - represents IP of sender node
        private const string NodeIpHeader = "X-Raft-Node-IP";
        //request - represents hosting port of sender node
        private const string NodePortHeader = "X-Raft-Node-Port";

        //request - represents Term value according with Raft protocol
        private const string TermHeader = "X-Raft-Term";

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

        private protected class InboundMessageContent : IMessage
        {
            private readonly ContentType contentType;
            private readonly long? length;
            private readonly string name;
            private readonly object content;

            internal InboundMessageContent(HttpRequest request)
            {
                length = request.ContentLength;
                contentType = new ContentType(request.ContentType);
                name = request.Headers[MessageNameHeader].FirstOrDefault() ??
                       throw new RaftProtocolException(ExceptionMessages.MissingHeader(MessageNameHeader));
                content = request.Body;
            }

            internal InboundMessageContent(HttpResponseMessage response)
            {
                length = response.Content.Headers.ContentLength;
                contentType = new ContentType(response.Content.Headers.ContentType.ToString());
                name = response.Headers.TryGetValues(MessageNameHeader, out var values)
                    ? values.FirstOrDefault()
                    : null;
                if (name is null)
                    throw new RaftProtocolException(ExceptionMessages.MissingHeader(MessageNameHeader));
                content = response.Content;
            }

            string IMessage.Name => name;
            long? IMessage.Length => length;

            Task IMessage.CopyToAsync(Stream output)
            {
                switch (content)
                {
                    case Stream stream:
                        return stream.CopyToAsync(output);
                    case HttpContent content:
                        return content.CopyToAsync(output);
                    default:
                        throw new InvalidOperationException();
                }
            }

            ContentType IMessage.Type => contentType;
        }

        internal readonly IPEndPoint Sender;
        internal readonly string MessageType;
        internal readonly long ConsensusTerm;

        private protected RaftHttpMessage(string messageType, IPEndPoint sender, long term)
        {
            Sender = sender;
            MessageType = messageType;
            ConsensusTerm = term;
        }

        private protected RaftHttpMessage(HttpRequest request)
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
            foreach (var header in request.Headers[TermHeader])
                if (long.TryParse(header, out ConsensusTerm))
                    break;
            MessageType = GetMessageType(request);
        }

        internal static string GetMessageType(HttpRequest request) =>
            request.Headers[MessageTypeHeader].FirstOrDefault() ??
            throw new RaftProtocolException(ExceptionMessages.MissingHeader(MessageTypeHeader));

        private protected virtual void FillRequest(HttpRequestMessage request)
        {
            request.Headers.Add(NodeIpHeader, Sender.Address.ToString());
            request.Headers.Add(NodePortHeader, Convert.ToString(Sender.Port, InvariantCulture));
            request.Headers.Add(TermHeader, Convert.ToString(ConsensusTerm, InvariantCulture));
            request.Headers.Add(MessageTypeHeader, MessageType);
            request.Method = HttpMethod.Post;
        }

        public static explicit operator HttpRequestMessage(RaftHttpMessage message)
        {
            if (message is null)
                return null;
            var request = new HttpRequestMessage { Method = HttpMethod.Post };
            message.FillRequest(request);
            return request;
        }

        private protected static LogEntryId? ParseLogEntryId(HttpRequest request, string indexHeader, string termHeader)
        {
            long? term = null, index = null;
            foreach (var header in request.Headers[indexHeader])
                if (long.TryParse(header, out var value))
                {
                    index = value;
                    break;
                }

            foreach (var header in request.Headers[termHeader])
                if (long.TryParse(header, out var value))
                {
                    term = value;
                    break;
                }

            return term is null || index is null ? default(LogEntryId?) : new LogEntryId(term.Value, index.Value);
        }
    }
}

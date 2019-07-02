using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Messaging;

    internal abstract class HttpMessage
    {
        private static readonly ValueParser<string> StringParser = delegate(string str, out string value)
        {
            value = str;
            return true;
        };

        private protected static readonly ValueParser<long> Int64Parser = long.TryParse;
        private static readonly ValueParser<int> Int32Parser = int.TryParse;
        private static readonly ValueParser<IPAddress> IpAddressParser = IPAddress.TryParse;
        private protected static readonly ValueParser<bool> BooleanParser = bool.TryParse;

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

        internal readonly IPEndPoint Sender;
        internal readonly string MessageType;

        private protected HttpMessage(string messageType, IPEndPoint sender)
        {
            Sender = sender;
            MessageType = messageType;
        }

        private protected HttpMessage(HeadersReader<StringValues> headers)
        {
            var address = ParseHeader(NodeIpHeader, headers, IpAddressParser);
            var port = ParseHeader(NodePortHeader, headers, Int32Parser);
            Sender = new IPEndPoint(address, port);
            MessageType = GetMessageType(headers);
        }

        private static string GetMessageType(HeadersReader<StringValues> headers) =>
            ParseHeader(MessageTypeHeader, headers);

        internal static string GetMessageType(HttpRequest request) => GetMessageType(request.Headers.TryGetValue);
            
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

        private protected static T ParseHeader<THeaders, T>(string headerName, HeadersReader<THeaders> reader,
            ValueParser<T> parser)
            where THeaders : IEnumerable<string>
        {
            if (reader(headerName, out var headers))
                foreach (var header in headers)
                    if (parser(header, out var result))
                        return result;

            throw new RaftProtocolException(ExceptionMessages.MissingHeader(headerName));
        }

        private protected static string ParseHeader<THeaders>(string headerName, HeadersReader<THeaders> reader)
            where THeaders : IEnumerable<string>
            => ParseHeader(headerName, reader, StringParser);
    }
}
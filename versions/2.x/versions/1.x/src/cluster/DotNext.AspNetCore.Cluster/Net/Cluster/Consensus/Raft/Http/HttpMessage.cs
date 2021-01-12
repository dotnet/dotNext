using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal abstract class HttpMessage
    {
        private static readonly ValueParser<string> StringParser = delegate (string str, out string value)
        {
            value = str;
            return true;
        };

        private protected static readonly ValueParser<long> Int64Parser = long.TryParse;
        private static readonly ValueParser<int> Int32Parser = int.TryParse;
        private static readonly ValueParser<IPAddress> IpAddressParser = IPAddress.TryParse;
        private protected static readonly ValueParser<bool> BooleanParser = bool.TryParse;
        private static readonly Random RequestIdGenerator = new Random();
        private const string RequestIdAllowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*-+=~";
        private const int RequestIdLength = 32;

        //request - represents IP of sender node
        private const string NodeIpHeader = "X-Raft-Node-IP";

        //request - represents hosting port of sender node
        private const string NodePortHeader = "X-Raft-Node-Port";

        //request - represents request message type
        private const string MessageTypeHeader = "X-Raft-Message-Type";

        //request - represents unique request identifier
        private const string RequestIdHeader = "X-Request-ID";

        private protected class OutboundTransferObject : HttpContent
        {
            private readonly IDataTransferObject dto;

            internal OutboundTransferObject(IDataTransferObject dto) => this.dto = dto;

            protected sealed override Task SerializeToStreamAsync(Stream stream, TransportContext context)
                => dto.CopyToAsync(stream);

            protected sealed override bool TryComputeLength(out long length)
                => dto.Length.TryGet(out length);
        }

        internal readonly string Id;
        internal readonly IPEndPoint Sender;
        internal readonly string MessageType;

        private protected HttpMessage(string messageType, IPEndPoint sender)
        {
            Sender = sender;
            MessageType = messageType;
            Id = RequestIdGenerator.NextString(RequestIdAllowedChars, RequestIdLength);
        }

        private protected HttpMessage(HeadersReader<StringValues> headers)
        {
            var address = ParseHeader(NodeIpHeader, headers, IpAddressParser);
            var port = ParseHeader(NodePortHeader, headers, Int32Parser);
            Sender = new IPEndPoint(address, port);
            MessageType = GetMessageType(headers);
            Id = ParseHeader(RequestIdHeader, headers);
        }

        private static string GetMessageType(HeadersReader<StringValues> headers) =>
            ParseHeader(MessageTypeHeader, headers);

        internal static string GetMessageType(HttpRequest request) => GetMessageType(request.Headers.TryGetValue);

        internal virtual void PrepareRequest(HttpRequestMessage request)
        {
            request.Headers.Add(NodeIpHeader, Sender.Address.ToString());
            request.Headers.Add(NodePortHeader, Sender.Port.ToString(InvariantCulture));
            request.Headers.Add(MessageTypeHeader, MessageType);
            request.Headers.Add(RequestIdHeader, Id);
            request.Method = HttpMethod.Post;
        }

        private protected static async Task<bool> ParseBoolResponse(HttpResponseMessage response)
            => bool.TryParse(await response.Content.ReadAsStringAsync().ConfigureAwait(false), out var result)
                ? result
                : throw new RaftProtocolException(ExceptionMessages.IncorrectResponse);

        private protected static Task SaveResponse(HttpResponse response, bool result, CancellationToken token)
        {
            response.StatusCode = StatusCodes.Status200OK;
            return response.WriteAsync(result.ToString(InvariantCulture), token);
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
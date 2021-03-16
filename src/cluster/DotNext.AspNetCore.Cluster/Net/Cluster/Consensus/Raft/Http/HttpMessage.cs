using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using IO;

    internal abstract class HttpMessage
    {
        private const string RequestIdAllowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*-+=~";
        private const int RequestIdLength = 32;

        // request - represents ID of sender node
        private const string NodeIdHeader = "X-Raft-Node-ID";

        // request - represents request message type
        private const string MessageTypeHeader = "X-Raft-Message-Type";

        // request - represents unique request identifier
        private const string RequestIdHeader = "X-Request-ID";

        private protected static readonly ValueParser<long> Int64Parser = long.TryParse;
        private protected static readonly ValueParser<int> Int32Parser = int.TryParse;
        private static readonly ValueParser<ClusterMemberId> IpAddressParser = ClusterMemberId.TryParse;
        private protected static readonly ValueParser<bool> BooleanParser = bool.TryParse;
        private static readonly Random RequestIdGenerator = new Random();

        private protected class OutboundTransferObject : HttpContent
        {
            private readonly IDataTransferObject dto;

            internal OutboundTransferObject(IDataTransferObject dto) => this.dto = dto;

            protected sealed override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
                => SerializeToStreamAsync(stream, context, CancellationToken.None);

#if NETCOREAPP3_1
            private
#else
            protected sealed override
#endif
            Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken token) => dto.WriteToAsync(stream, token: token).AsTask();

            protected sealed override bool TryComputeLength(out long length)
                => dto.Length.TryGetValue(out length);
        }

        internal readonly string Id;
        internal readonly ClusterMemberId Sender;
        internal readonly string MessageType;

        private protected HttpMessage(string messageType, in ClusterMemberId sender)
        {
            Sender = sender;
            MessageType = messageType;
            Id = RequestIdGenerator.NextString(RequestIdAllowedChars, RequestIdLength);
        }

        private protected HttpMessage(HeadersReader<StringValues> headers)
        {
            Sender = ParseHeader(NodeIdHeader, headers, IpAddressParser);
            MessageType = GetMessageType(headers);
            Id = ParseHeader(RequestIdHeader, headers);
        }

        /// <summary>
        /// Interprets <see cref="HttpRequestException"/> produced by HTTP client.
        /// </summary>
        /// <returns><see langword="true"/> to handle the response as <see cref="MemberUnavailableException"/>.</returns>
        internal virtual bool IsMemberUnavailable(HttpStatusCode? code)
            => code is null || code.GetValueOrDefault() == HttpStatusCode.InternalServerError;

        private static string GetMessageType(HeadersReader<StringValues> headers) =>
            ParseHeader(MessageTypeHeader, headers);

        internal static string GetMessageType(HttpRequest request) => GetMessageType(request.Headers.TryGetValue);

        internal virtual void PrepareRequest(HttpRequestMessage request)
        {
            request.Headers.Add(NodeIdHeader, Sender.ToString());
            request.Headers.Add(MessageTypeHeader, MessageType);
            request.Headers.Add(RequestIdHeader, Id);
            request.Method = HttpMethod.Post;
        }

        private protected static async Task<bool> ParseBoolResponse(HttpResponseMessage response, CancellationToken token)
#if NETCOREAPP3_1
            => bool.TryParse(await response.Content.ReadAsStringAsync().ConfigureAwait(false), out var result)
#else
            => bool.TryParse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false), out var result)
#endif
                ? result
                : throw new RaftProtocolException(ExceptionMessages.IncorrectResponse);

        private protected static Task SaveResponse(HttpResponse response, bool result, CancellationToken token)
        {
            response.StatusCode = StatusCodes.Status200OK;
            return response.WriteAsync(result.ToString(InvariantCulture), token);
        }

        private protected static T ParseHeader<THeaders, T>(string headerName, HeadersReader<THeaders> reader, ValueParser<T> parser)
            where THeaders : IEnumerable<string>
        {
            if (reader(headerName, out var headers))
            {
                foreach (var header in headers)
                {
                    if (parser(header, out var result))
                        return result;
                }
            }

            throw new RaftProtocolException(ExceptionMessages.MissingHeader(headerName));
        }

        private protected static T? ParseHeaderAsNullable<THeaders, T>(string headerName, HeadersReader<THeaders> reader, ValueParser<T> parser)
            where THeaders : IEnumerable<string>
            where T : struct
        {
            if (reader(headerName, out var headers))
            {
                foreach (var header in headers)
                {
                    if (parser(header, out var result))
                        return result;
                }
            }

            return null;
        }

        private protected static string ParseHeader<THeaders>(string headerName, HeadersReader<THeaders> reader)
            where THeaders : IEnumerable<string>
            => ParseHeader(headerName, reader, static (string str, out string value) =>
        {
            value = str;
            return true;
        });
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using IO;
    using Messaging;
    using NullMessage = Threading.Tasks.CompletedTask<Messaging.IMessage?, Generic.DefaultConst<Messaging.IMessage>>;

    internal class CustomMessage : HttpMessage, IHttpMessageWriter<IMessage>, IHttpMessageReader<IMessage?>
    {
        // request - represents custom message name
        private const string MessageNameHeader = "X-Raft-Message-Name";
        private static readonly ValueParser<DeliveryMode> DeliveryModeParser = Enum.TryParse;

        internal enum DeliveryMode
        {
            OneWayNoAck,
            OneWay,
            RequestReply,
        }

        private sealed class OutboundMessageContent : OutboundTransferObject
        {
            internal OutboundMessageContent(IMessage message)
                : base(message)
            {
                Headers.ContentType = MediaTypeHeaderValue.Parse(message.Type.ToString());
                Headers.Add(MessageNameHeader, message.Name);
            }
        }

        private sealed class InboundMessageContent : IMessage
        {
            private readonly Stream requestStream;
            private readonly long? length;

            internal InboundMessageContent(Stream content, string name, ContentType type, long? length)
            {
                requestStream = content;
                Name = name;
                Type = type;
                this.length = length;
            }

            public string Name { get; }

            public ContentType Type { get; }

            bool IDataTransferObject.IsReusable => false;

            long? IDataTransferObject.Length
            {
                get
                {
                    if (length.HasValue)
                        return length.GetValueOrDefault();
                    if (requestStream.CanSeek)
                        return requestStream.Length;
                    return null;
                }
            }

            ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
                => new ValueTask(writer.CopyFromAsync(requestStream, token));

            ValueTask<TResult> IDataTransferObject.TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
                => IDataTransferObject.TransformAsync<TResult, TTransformation>(requestStream, transformation, false, token);
        }

        internal new const string MessageType = "CustomMessage";
        private const string DeliveryModeHeader = "X-Delivery-Type";

        private const string RespectLeadershipHeader = "X-Respect-Leadership";

        internal readonly DeliveryMode Mode;
        internal readonly IMessage Message;
        internal bool RespectLeadership;

        private protected CustomMessage(in ClusterMemberId sender, IMessage message, DeliveryMode mode)
            : base(MessageType, sender)
        {
            Message = message;
            Mode = mode;
        }

        internal CustomMessage(in ClusterMemberId sender, IMessage message, bool requiresConfirmation)
            : this(sender, message, requiresConfirmation ? DeliveryMode.OneWay : DeliveryMode.OneWayNoAck)
        {
        }

        private CustomMessage(HeadersReader<StringValues> headers, Stream body, ContentType contentType, long? length)
            : base(headers)
        {
            Mode = ParseHeader(DeliveryModeHeader, headers, DeliveryModeParser);
            RespectLeadership = ParseHeader(RespectLeadershipHeader, headers, BooleanParser);
            Message = new InboundMessageContent(body, ParseHeader(MessageNameHeader, headers), contentType, length);
        }

        internal CustomMessage(HttpRequest request)
            : this(request.Headers.TryGetValue, request.Body, new ContentType(request.ContentType), request.ContentLength)
        {
        }

        internal sealed override void PrepareRequest(HttpRequestMessage request)
        {
            request.Headers.Add(DeliveryModeHeader, Mode.ToString());
            request.Headers.Add(RespectLeadershipHeader, RespectLeadership.ToString(InvariantCulture));
            request.Content = new OutboundMessageContent(Message);
            base.PrepareRequest(request);
        }

        internal static async Task SaveResponse(HttpResponse response, IMessage message, CancellationToken token)
        {
            response.StatusCode = StatusCodes.Status200OK;
            response.ContentType = message.Type.ToString();
            response.ContentLength = message.Length;
            response.Headers.Add(MessageNameHeader, message.Name);
            await response.StartAsync(token).ConfigureAwait(false);
            await message.WriteToAsync(response.BodyWriter, token).ConfigureAwait(false);
            await response.BodyWriter.FlushAsync(token).ConfigureAwait(false);
        }

        Task IHttpMessageWriter<IMessage>.SaveResponse(HttpResponse response, IMessage message, CancellationToken token)
            => SaveResponse(response, message, token);

        // do not parse response because this is one-way message
        Task<IMessage?> IHttpMessageReader<IMessage?>.ParseResponse(HttpResponseMessage response, CancellationToken token) => NullMessage.Task;

        private protected static async Task<T> ParseResponse<T>(HttpResponseMessage response, MessageReader<T> reader, CancellationToken token)
        {
            var contentType = response.Content.Headers.ContentType?.ToString();
            var name = ParseHeader<IEnumerable<string>>(MessageNameHeader, response.Headers.TryGetValues);
#if NETCOREAPP3_1
            await using var content = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
#else
            await using var content = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
#endif
            return await reader(new InboundMessageContent(content, name, string.IsNullOrEmpty(contentType) ? new ContentType() : new ContentType(contentType), response.Content.Headers.ContentLength), token).ConfigureAwait(false);
        }
    }

    internal sealed class CustomMessage<T> : CustomMessage, IHttpMessageReader<T>
    {
        private readonly MessageReader<T> reader;

        internal CustomMessage(ClusterMemberId sender, IMessage message, MessageReader<T> reader)
            : base(sender, message, DeliveryMode.RequestReply) => this.reader = reader;

        Task<T> IHttpMessageReader<T>.ParseResponse(HttpResponseMessage response, CancellationToken token)
            => ParseResponse<T>(response, reader, token);
    }
}

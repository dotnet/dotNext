using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Runtime.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

using IO;
using Messaging;
using Runtime.Serialization;
using static IO.Pipelines.ResultExtensions;

internal class CustomMessage : HttpMessage, IHttpMessage<IMessage?>
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

    private sealed class OutboundMessageContent : HttpContent
    {
        private readonly IDataTransferObject dto;

        internal OutboundMessageContent(IMessage message)
        {
            Headers.ContentType = MediaTypeHeaderValue.Parse(message.Type.ToString());
            Headers.Add(MessageNameHeader, message.Name);
            dto = message;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken token) => dto.WriteToAsync(stream, token: token).AsTask();

        protected override bool TryComputeLength(out long length)
            => dto.Length.TryGetValue(out length);
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
            => new(writer.CopyFromAsync(requestStream, token));

        ValueTask<TResult> IDataTransferObject.TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
            => IDataTransferObject.TransformAsync<TResult, TTransformation>(requestStream, transformation, false, token);
    }

    internal const string MessageType = "CustomMessage";
    private const string DeliveryModeHeader = "X-Delivery-Type";

    private const string RespectLeadershipHeader = "X-Respect-Leadership";

    internal readonly DeliveryMode Mode;
    internal readonly IMessage Message;
    internal bool RespectLeadership;

    private protected CustomMessage(in ClusterMemberId sender, IMessage message, DeliveryMode mode)
        : base(sender)
    {
        Message = message;
        Mode = mode;
    }

    internal CustomMessage(in ClusterMemberId sender, IMessage message, bool requiresConfirmation)
        : this(sender, message, requiresConfirmation ? DeliveryMode.OneWay : DeliveryMode.OneWayNoAck)
    {
    }

    private CustomMessage(IDictionary<string, StringValues> headers, Stream body, ContentType contentType, long? length)
        : base(headers)
    {
        Mode = ParseHeader(headers, DeliveryModeHeader, DeliveryModeParser);
        RespectLeadership = ParseHeader(headers, RespectLeadershipHeader, BooleanParser);
        Message = new InboundMessageContent(body, ParseHeader(headers, MessageNameHeader), contentType, length);
    }

    internal CustomMessage(HttpRequest request)
        : this(request.Headers, request.Body, new ContentType(request.ContentType ?? MediaTypeNames.Application.Octet), request.ContentLength)
    {
    }

    public new void PrepareRequest(HttpRequestMessage request)
    {
        request.Headers.Add(DeliveryModeHeader, Mode.ToString());
        request.Headers.Add(RespectLeadershipHeader, RespectLeadership.ToString(InvariantCulture));
        request.Content = new OutboundMessageContent(Message);
        base.PrepareRequest(request);
    }

    internal static async Task SaveResponseAsync(HttpResponse response, IMessage message, CancellationToken token)
    {
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = message.Type.ToString();
        response.ContentLength = message.Length;
        response.Headers.Add(MessageNameHeader, message.Name);
        await response.StartAsync(token).ConfigureAwait(false);
        await message.WriteToAsync(response.BodyWriter, token).ConfigureAwait(false);
        var result = await response.BodyWriter.FlushAsync(token).ConfigureAwait(false);
        result.ThrowIfCancellationRequested(token);
    }

    // do not parse response because this is one-way message
    Task<IMessage?> IHttpMessage<IMessage?>.ParseResponseAsync(HttpResponseMessage response, CancellationToken token)
        => token.IsCancellationRequested ? Task.FromCanceled<IMessage?>(token) : Task.FromResult<IMessage?>(null);

    private protected static async Task<T> ParseResponseAsync<T>(HttpResponseMessage response, MessageReader<T> reader, CancellationToken token)
    {
        var contentType = response.Content.Headers.ContentType?.ToString();
        var name = ParseHeader(response.Headers, MessageNameHeader);
        var content = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        try
        {
            return await reader(new InboundMessageContent(content, name, string.IsNullOrEmpty(contentType) ? new ContentType() : new ContentType(contentType), response.Content.Headers.ContentLength), token).ConfigureAwait(false);
        }
        finally
        {
            await content.DisposeAsync().ConfigureAwait(false);
        }
    }

    [RequiresPreviewFeatures]
    private protected static async Task<T> ParseResponseAsync<T>(HttpResponseMessage response, CancellationToken token)
        where T : notnull, ISerializable<T>
    {
        var content = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        try
        {
            return await Serializable.ReadFromAsync<T>(content, token: token).ConfigureAwait(false);
        }
        finally
        {
            await content.DisposeAsync().ConfigureAwait(false);
        }
    }

    [RequiresPreviewFeatures]
    static string IHttpMessage.MessageType => MessageType;
}

internal sealed class CustomMessage<T> : CustomMessage, IHttpMessage<T>
{
    private readonly MessageReader<T> reader;

    internal CustomMessage(in ClusterMemberId sender, IMessage message, MessageReader<T> reader)
        : base(sender, message, DeliveryMode.RequestReply) => this.reader = reader;

    Task<T> IHttpMessage<T>.ParseResponseAsync(HttpResponseMessage response, CancellationToken token)
        => ParseResponseAsync<T>(response, reader, token);
}

internal sealed class CustomSerializableMessage<T> : CustomMessage, IHttpMessage<T>
    where T : notnull, ISerializable<T>
{
    internal CustomSerializableMessage(in ClusterMemberId sender, IMessage message)
        : base(sender, message, DeliveryMode.RequestReply)
    {
    }

    [RequiresPreviewFeatures]
    Task<T> IHttpMessage<T>.ParseResponseAsync(HttpResponseMessage response, CancellationToken token)
        => ParseResponseAsync<T>(response, token);
}
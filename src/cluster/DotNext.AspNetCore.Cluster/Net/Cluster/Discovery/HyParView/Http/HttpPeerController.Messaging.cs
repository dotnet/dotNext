using System.Net;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;

namespace DotNext.Net.Cluster.Discovery.HyParView.Http;

internal partial class HttpPeerController
{
    private const string MessageTypeHeader = "X-HyParView-Message-Type";

    private static string GetMessageType(HttpRequest request)
    {
        string? result = null;

        if (request.Headers.TryGetValue(MessageTypeHeader, out var values))
            result = values[0];

        return result ?? string.Empty;
    }

    internal Task ProcessRequest(HttpContext context)
    {
        var length = context.Request.ContentLength;
        if (length is null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return Task.CompletedTask;
        }

        if (length.GetValueOrDefault() > int.MaxValue)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return Task.CompletedTask;
        }

        Task result;
        switch (GetMessageType(context.Request))
        {
            case JoinMessageType:
                result = ProcessJoinAsync(context.Request, context.Response, (int)length.GetValueOrDefault(), context.RequestAborted);
                break;
            case ForwardJoinMessageType:
                result = ProcessForwardJoinAsync(context.Request, context.Response, (int)length.GetValueOrDefault(), context.RequestAborted);
                break;
            case NeighborMessageType:
                result = ProcessNeighborAsync(context.Request, context.Response, (int)length.GetValueOrDefault(), context.RequestAborted);
                break;
            case DisconnectMessageType:
                result = ProcessDisconnectAsync(context.Request, context.Response, (int)length.GetValueOrDefault(), context.RequestAborted);
                break;
            case ShuffleMessageType:
                result = ProcessShuffleRequestAsync(context.Request, context.Response, (int)length.GetValueOrDefault(), context.RequestAborted);
                break;
            case ShuffleReplyMessageType:
                result = ProcessShuffleReplyAsync(context.Request, context.Response, (int)length.GetValueOrDefault(), context.RequestAborted);
                break;
            default:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                result = Task.CompletedTask;
                break;
        }

        return result;
    }

    private async Task PostAsync<TRequest>(EndPoint peer, string messageType, TRequest content, CancellationToken token)
        where TRequest : ISupplier<ReadOnlyMemory<byte>>
    {
        var client = GetOrCreatePeer((UriEndPoint)peer);

        using var request = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            Version = client.DefaultRequestVersion,
            VersionPolicy = client.DefaultVersionPolicy,
            RequestUri = resourcePath,
        };

        request.Headers.Add(MessageTypeHeader, messageType);
        request.Content = new ReadOnlyMemoryContent(content.Invoke());

        using var response = await client.SendAsync(request, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
}
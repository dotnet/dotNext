using System.Runtime.Versioning;
using Microsoft.AspNetCore.Http;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

internal sealed class ResignMessage : HttpMessage, IHttpMessage<bool>
{
    internal const string MessageType = "Resign";

    internal ResignMessage(in ClusterMemberId sender)
        : base(sender)
    {
    }

    internal ResignMessage(HttpRequest request)
        : base(request.Headers)
    {
    }

    Task<bool> IHttpMessage<bool>.ParseResponseAsync(HttpResponseMessage response, CancellationToken token) => ParseBoolResponseAsync(response, token);

    [RequiresPreviewFeatures]
    static string IHttpMessage.MessageType => MessageType;

    void IHttpMessage.PrepareRequest(HttpRequestMessage request) => PrepareRequest(request);

    internal static new Task SaveResponseAsync(HttpResponse response, bool result, CancellationToken token) => HttpMessage.SaveResponseAsync(response, result, token);
}
using Microsoft.AspNetCore.Http;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

internal sealed class ResignMessage : HttpMessage, IHttpMessageReader<bool>
{
    internal new const string MessageType = "Resign";

    internal ResignMessage(in ClusterMemberId sender)
        : base(MessageType, sender)
    {
    }

    internal ResignMessage(HttpRequest request)
        : base(request.Headers)
    {
    }

    Task<bool> IHttpMessageReader<bool>.ParseResponseAsync(HttpResponseMessage response, CancellationToken token) => ParseBoolResponseAsync(response, token);

    internal static new Task SaveResponseAsync(HttpResponse response, bool result, CancellationToken token) => HttpMessage.SaveResponseAsync(response, result, token);
}
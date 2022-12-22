using Microsoft.AspNetCore.Http;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

internal sealed class ResignMessage : HttpMessage, IHttpMessageReader<bool>, IHttpMessageWriter<bool>
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

    Task<bool> IHttpMessageReader<bool>.ParseResponse(HttpResponseMessage response, CancellationToken token) => ParseBoolResponse(response, token);

    public new Task SaveResponse(HttpResponse response, bool result, CancellationToken token) => HttpMessage.SaveResponse(response, result, token);
}
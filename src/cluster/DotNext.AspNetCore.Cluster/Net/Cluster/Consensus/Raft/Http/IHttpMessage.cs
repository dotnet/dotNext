using Microsoft.AspNetCore.Http;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

internal interface IHttpMessageReader<TContent>
{
    Task<TContent> ParseResponse(HttpResponseMessage response, CancellationToken token);
}

internal interface IHttpMessageWriter<in TContent>
{
    Task SaveResponse(HttpResponse response, TContent result, CancellationToken token);
}
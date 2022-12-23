namespace DotNext.Net.Cluster.Consensus.Raft.Http;

internal interface IHttpMessageReader<TContent>
{
    Task<TContent> ParseResponseAsync(HttpResponseMessage response, CancellationToken token);
}
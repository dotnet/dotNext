using HttpStatusCode = System.Net.HttpStatusCode;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

internal interface IHttpMessage
{
    static abstract string MessageType { get; }

    void PrepareRequest(HttpRequestMessage request);

    /// <summary>
    /// Interprets <see cref="HttpRequestException"/> produced by HTTP client.
    /// </summary>
    /// <returns><see langword="true"/> to handle the response as <see cref="MemberUnavailableException"/>.</returns>
    static abstract bool IsMemberUnavailable(HttpStatusCode? code);
}

internal interface IHttpMessage<TResponse> : IHttpMessage
{
    Task<TResponse> ParseResponseAsync(HttpResponseMessage response, CancellationToken token);
}
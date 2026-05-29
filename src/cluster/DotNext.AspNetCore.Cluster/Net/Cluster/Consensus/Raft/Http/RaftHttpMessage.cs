using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

internal abstract class RaftHttpMessage : HttpMessage
{
    // request - represents Term value according to Raft protocol
    // response - represents Term value of the reply node
    private const string TermHeader = "X-Raft-Term";

    internal readonly long ConsensusTerm;

    private protected RaftHttpMessage(in ClusterMemberId sender, long term)
        : base(sender) => ConsensusTerm = term;

    private protected RaftHttpMessage(IDictionary<string, StringValues> headers)
        : base(headers)
    {
        ConsensusTerm = ParseHeader(headers, TermHeader, Int64Parser);
    }

    protected new void PrepareRequest(HttpRequestMessage request)
    {
        request.Headers.Add(TermHeader, ConsensusTerm.ToString(InvariantCulture));
        base.PrepareRequest(request);
    }

    // serves as a default implementation of IHttpMessage.IsMemberUnavailable
    public new static bool IsMemberUnavailable(HttpStatusCode? code) => true;

    private protected new static async Task<Result<bool>> ParseBoolResponseAsync(HttpResponseMessage response, CancellationToken token) => new()
    {
        Value = await HttpMessage.ParseBoolResponseAsync(response, token).ConfigureAwait(false),
        Term = ParseHeader(response.Headers, TermHeader, Int64Parser),
    };

    private protected new static async Task<Result<T>> ParseEnumResponseAsync<T>(HttpResponseMessage response, CancellationToken token)
        where T : struct, Enum => new()
        {
            Value = await HttpMessage.ParseEnumResponseAsync<T>(response, token).ConfigureAwait(false),
            Term = ParseHeader(response.Headers, TermHeader, Int64Parser),
        };

    private protected static Task SaveResponseAsync(HttpResponse response, in Result<bool> result, CancellationToken token)
    {
        response.Headers.Append(TermHeader, result.Term.ToString(InvariantCulture));
        return SaveResponseAsync(response, result.Value, token);
    }

    private protected static Task SaveResponseAsync<T>(HttpResponse response, in Result<T> result, CancellationToken token)
        where T : struct, Enum
    {
        response.Headers.Append(TermHeader, result.Term.ToString(InvariantCulture));
        return SaveResponseAsync(response, result.Value, token);
    }
}
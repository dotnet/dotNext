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
    
    // request - represents state machine version
    private const string StateVersionHeader = "X-Raft-State-Version";

    internal readonly int StateVersion;
    internal readonly long ConsensusTerm;

    private protected RaftHttpMessage(in ClusterMemberId sender, long term, int stateVersion)
        : base(sender)
    {
        ConsensusTerm = term;
        StateVersion = stateVersion;
    }

    private protected RaftHttpMessage(IDictionary<string, StringValues> headers)
        : base(headers)
    {
        ConsensusTerm = ParseHeader(headers, TermHeader, Int64Parser);
        StateVersion = ParseHeader(headers, StateVersionHeader, Int32Parser);
    }

    protected new void PrepareRequest(HttpRequestMessage request)
    {
        request.Headers.Add(TermHeader, ConsensusTerm.ToString(InvariantCulture));
        request.Headers.Add(StateVersionHeader, StateVersion.ToString(InvariantCulture));
        base.PrepareRequest(request);
    }

    // serves as a default implementation of IHttpMessage.IsMemberUnavailable
    public new static bool IsMemberUnavailable(HttpStatusCode? code) => true;

    private protected static long ParseTerm(HttpResponseMessage response)
        => ParseHeader(response.Headers, TermHeader, Int64Parser);

    private protected new static async Task<Result<bool>> ParseBoolResponseAsync(HttpResponseMessage response, CancellationToken token) => new()
    {
        Value = await HttpMessage.ParseBoolResponseAsync(response, token).ConfigureAwait(false),
        Term = ParseTerm(response),
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

    private protected static void WriteTerm(HttpResponse response, long term)
        => response.Headers.Append(TermHeader, term.ToString(InvariantCulture));

    private protected static Task SaveResponseAsync<T>(HttpResponse response, in Result<T> result, CancellationToken token)
        where T : struct, Enum
    {
        WriteTerm(response, result.Term);
        return SaveResponseAsync(response, result.Value, token);
    }
}
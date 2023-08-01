using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using static System.Globalization.CultureInfo;
using HeaderUtils = Microsoft.Net.Http.Headers.HeaderUtilities;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

internal abstract class RaftHttpMessage : HttpMessage
{
    // cached to avoid memory allocation
    private protected static readonly ValueParser<DateTimeOffset> Rfc1123Parser = TryParseRfc1123FormattedDateTime;

    // request - represents Term value according with Raft protocol
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
    public static new bool IsMemberUnavailable(HttpStatusCode? code) => true;

    private static bool TryParseRfc1123FormattedDateTime(string input, out DateTimeOffset result)
        => HeaderUtils.TryParseDate(input, out result);

    private protected static new async Task<Result<bool>> ParseBoolResponseAsync(HttpResponseMessage response, CancellationToken token) => new()
    {
        Value = await HttpMessage.ParseBoolResponseAsync(response, token).ConfigureAwait(false),
        Term = ParseHeader(response.Headers, TermHeader, Int64Parser),
    };

    private protected static new async Task<Result<T>> ParseEnumResponseAsync<T>(HttpResponseMessage response, CancellationToken token)
        where T : struct, Enum => new()
        {
            Value = await HttpMessage.ParseEnumResponseAsync<T>(response, token).ConfigureAwait(false),
            Term = ParseHeader(response.Headers, TermHeader, Int64Parser),
        };

    private protected static Task SaveResponseAsync(HttpResponse response, in Result<bool> result, CancellationToken token)
    {
        response.Headers.Add(TermHeader, result.Term.ToString(InvariantCulture));
        return HttpMessage.SaveResponseAsync(response, result.Value, token);
    }

    private protected static Task SaveResponseAsync<T>(HttpResponse response, in Result<T> result, CancellationToken token)
        where T : struct, Enum
    {
        response.Headers.Add(TermHeader, result.Term.ToString(InvariantCulture));
        return HttpMessage.SaveResponseAsync(response, result.Value, token);
    }
}
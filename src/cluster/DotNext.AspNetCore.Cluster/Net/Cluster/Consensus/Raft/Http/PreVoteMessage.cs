using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

internal sealed class PreVoteMessage : RaftHttpMessage, IHttpMessageReader<Result<PreVoteResult>>, IHttpMessageWriter<Result<PreVoteResult>>
{
    internal new const string MessageType = "PreVote";

    internal readonly long LastLogIndex;
    internal readonly long LastLogTerm;

    internal PreVoteMessage(in ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm)
        : base(MessageType, sender, term)
    {
        LastLogIndex = lastLogIndex;
        LastLogTerm = lastLogTerm;
    }

    private PreVoteMessage(HeadersReader<StringValues> headers)
        : base(headers)
    {
        LastLogIndex = ParseHeader(RequestVoteMessage.RecordIndexHeader, headers, Int64Parser);
        LastLogTerm = ParseHeader(RequestVoteMessage.RecordTermHeader, headers, Int64Parser);
    }

    internal PreVoteMessage(HttpRequest request)
        : this(request.Headers.TryGetValue)
    {
    }

    internal override void PrepareRequest(HttpRequestMessage request)
    {
        request.Headers.Add(RequestVoteMessage.RecordIndexHeader, LastLogIndex.ToString(InvariantCulture));
        request.Headers.Add(RequestVoteMessage.RecordTermHeader, LastLogTerm.ToString(InvariantCulture));
        base.PrepareRequest(request);
    }

    Task<Result<PreVoteResult>> IHttpMessageReader<Result<PreVoteResult>>.ParseResponse(HttpResponseMessage response, CancellationToken token) => ParseEnumResponse<PreVoteResult>(response, token);

    public Task SaveResponse(HttpResponse response, Result<PreVoteResult> result, CancellationToken token) => RaftHttpMessage.SaveResponse(response, result, token);
}
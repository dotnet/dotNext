using System.Runtime.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

internal sealed class PreVoteMessage : RaftHttpMessage, IHttpMessage<Result<PreVoteResult>>
{
    internal const string MessageType = "PreVote";

    internal readonly long LastLogIndex;
    internal readonly long LastLogTerm;

    internal PreVoteMessage(in ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm)
        : base(sender, term)
    {
        LastLogIndex = lastLogIndex;
        LastLogTerm = lastLogTerm;
    }

    private PreVoteMessage(IDictionary<string, StringValues> headers)
        : base(headers)
    {
        LastLogIndex = ParseHeader(headers, RequestVoteMessage.RecordIndexHeader, Int64Parser);
        LastLogTerm = ParseHeader(headers, RequestVoteMessage.RecordTermHeader, Int64Parser);
    }

    internal PreVoteMessage(HttpRequest request)
        : this(request.Headers)
    {
    }

    public new void PrepareRequest(HttpRequestMessage request)
    {
        request.Headers.Add(RequestVoteMessage.RecordIndexHeader, LastLogIndex.ToString(InvariantCulture));
        request.Headers.Add(RequestVoteMessage.RecordTermHeader, LastLogTerm.ToString(InvariantCulture));
        base.PrepareRequest(request);
    }

    Task<Result<PreVoteResult>> IHttpMessage<Result<PreVoteResult>>.ParseResponseAsync(HttpResponseMessage response, CancellationToken token) => ParseEnumResponseAsync<PreVoteResult>(response, token);

    [RequiresPreviewFeatures]
    static string IHttpMessage.MessageType => MessageType;

    internal static Task SaveResponseAsync(HttpResponse response, Result<PreVoteResult> result, CancellationToken token) => RaftHttpMessage.SaveResponseAsync(response, result, token);
}
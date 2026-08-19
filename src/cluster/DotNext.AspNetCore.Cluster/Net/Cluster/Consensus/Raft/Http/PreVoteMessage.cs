using Microsoft.AspNetCore.Http;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

internal sealed class PreVoteMessage : VoteMessageBase, IHttpMessage<Result<PreVoteResult>>
{
    internal const string MessageType = "PreVote";

    internal PreVoteMessage(in ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm, int stateVersion)
        : base(sender, term, lastLogIndex, lastLogTerm, stateVersion)
    {
    }

    internal PreVoteMessage(HttpRequest request)
        : base(request)
    {
    }

    Task<Result<PreVoteResult>> IHttpMessage<Result<PreVoteResult>>.ParseResponseAsync(HttpResponseMessage response, CancellationToken token) => ParseEnumResponseAsync<PreVoteResult>(response, token);

    static string IHttpMessage.MessageType => MessageType;

    internal static Task SaveResponseAsync(HttpResponse response, Result<PreVoteResult> result, CancellationToken token) => RaftHttpMessage.SaveResponseAsync(response, result, token);
}
using Microsoft.AspNetCore.Http;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

internal sealed class RequestVoteMessage : VoteMessageBase, IHttpMessage<Result<bool>>
{
    internal const string MessageType = "RequestVote";

    internal RequestVoteMessage(in ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm, int stateVersion)
        : base(sender, term, lastLogIndex, lastLogTerm, stateVersion)
    {
    }

    internal RequestVoteMessage(HttpRequest request)
        : base(request)
    {
    }

    Task<Result<bool>> IHttpMessage<Result<bool>>.ParseResponseAsync(HttpResponseMessage response, CancellationToken token) => ParseBoolResponseAsync(response, token);

    static string IHttpMessage.MessageType => MessageType;

    internal static Task SaveResponseAsync(HttpResponse response, Result<bool> result, CancellationToken token) => RaftHttpMessage.SaveResponseAsync(response, result, token);
}
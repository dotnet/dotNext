using System.Net.Http;
using Microsoft.AspNetCore.Http;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

public sealed class RaftHttpMessageTests : Test
{
    private static HttpRequest CreateVoteRequest(string stateVersion)
    {
        var message = new RequestVoteMessage(default, term: 2L, lastLogIndex: 5L, lastLogTerm: 1L, stateVersion: 0);
        using var outgoing = new HttpRequestMessage();
        message.PrepareRequest(outgoing);
        var request = new DefaultHttpContext().Request;
        foreach (var header in outgoing.Headers)
            request.Headers[header.Key] = header.Value.ToArray();

        if (stateVersion is null)
            request.Headers.Remove("X-Raft-State-Version");
        else
            request.Headers["X-Raft-State-Version"] = stateVersion;

        return request;
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("0", 0)]
    [InlineData("42", 42)]
    public static void LegacyStateVersionDefaultsToZero(string header, int expected)
    {
        var message = new RequestVoteMessage(CreateVoteRequest(header));
        Equal(expected, message.StateVersion);
        Equal(2L, message.ConsensusTerm);
        Equal(5L, message.LastLogIndex);
    }

    [Fact]
    public static void MalformedStateVersionIsRejected()
        => Throws<RaftProtocolException>(() => new RequestVoteMessage(CreateVoteRequest("invalid")));

    private static IHttpMessage<Result<ReplicationStatus>> CreateAppendRequest()
        => new AppendEntriesMessage<EmptyLogEntry, EmptyLogEntry[]>(default, term: 2L,
            prevLogIndex: 5L, prevLogTerm: 1L, commitIndex: 4L, entries: [], stateVersion: 0);

    private static HttpResponseMessage CreateAppendResponse(HeartbeatResult result, string lastIndex)
    {
        var response = new HttpResponseMessage { Content = new StringContent(result.ToString()) };
        response.Headers.Add("X-Raft-Term", "2");
        if (lastIndex is not null)
            response.Headers.Add("X-Raft-Last-Index", lastIndex);

        return response;
    }

    [Theory]
    [InlineData(HeartbeatResult.Rejected, null, 5L)]
    [InlineData(HeartbeatResult.ReplicatedWithLeaderTerm, null, 5L)]
    [InlineData(HeartbeatResult.Rejected, "3", 3L)]
    [InlineData(HeartbeatResult.ReplicatedWithLeaderTerm, "7", 7L)]
    public static async Task LegacyAppendResponseFallsBackToPreviousIndex(HeartbeatResult result, string header, long expected)
    {
        using var response = CreateAppendResponse(result, header);
        var parsed = await CreateAppendRequest().ParseResponseAsync(response, TestContext.Current.CancellationToken);
        Equal(2L, parsed.Term);
        Equal(result, parsed.Value.Result);
        Equal(expected, parsed.Value.LastIndex);
    }

    [Fact]
    public static async Task MalformedLastIndexIsRejected()
    {
        using var response = CreateAppendResponse(HeartbeatResult.Rejected, "invalid");
        await ThrowsAsync<RaftProtocolException>(() => CreateAppendRequest()
            .ParseResponseAsync(response, TestContext.Current.CancellationToken));
    }
}

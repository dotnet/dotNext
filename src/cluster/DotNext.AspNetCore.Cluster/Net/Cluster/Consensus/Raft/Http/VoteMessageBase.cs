using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

internal abstract class VoteMessageBase : RaftHttpMessage
{
    private const string RecordIndexHeader = "X-Raft-Record-Index";
    internal const string RecordTermHeader = "X-Raft-Record-Term";

    internal readonly long LastLogIndex;
    internal readonly long LastLogTerm;

    protected VoteMessageBase(in ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm, int stateVersion)
        : base(sender, term, stateVersion)
    {
        LastLogIndex = lastLogIndex;
        LastLogTerm = lastLogTerm;
    }

    private VoteMessageBase(IDictionary<string, StringValues> headers)
        : base(headers)
    {
        LastLogIndex = ParseHeader(headers, RecordIndexHeader, Int64Parser);
        LastLogTerm = ParseHeader(headers, RecordTermHeader, Int64Parser);
    }

    protected VoteMessageBase(HttpRequest request)
        : this(request.Headers)
    {
    }

    public new void PrepareRequest(HttpRequestMessage request)
    {
        request.Headers.Add(RecordIndexHeader, LastLogIndex.ToString(InvariantCulture));
        request.Headers.Add(RecordTermHeader, LastLogTerm.ToString(InvariantCulture));
        base.PrepareRequest(request);
    }
}
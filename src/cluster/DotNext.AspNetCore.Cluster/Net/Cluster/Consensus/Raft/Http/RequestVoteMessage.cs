using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class RequestVoteMessage : RaftHttpMessage, IHttpMessageReader<Result<bool>>, IHttpMessageWriter<Result<bool>>
    {
        internal const string RecordIndexHeader = "X-Raft-Record-Index";
        internal const string RecordTermHeader = "X-Raft-Record-Term";
        internal new const string MessageType = "RequestVote";

        internal readonly long LastLogIndex;
        internal readonly long LastLogTerm;

        internal RequestVoteMessage(ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm)
            : base(MessageType, sender, term)
        {
            LastLogIndex = lastLogIndex;
            LastLogTerm = lastLogTerm;
        }

        private RequestVoteMessage(HeadersReader<StringValues> headers)
            : base(headers)
        {
            LastLogIndex = ParseHeader(RecordIndexHeader, headers, Int64Parser);
            LastLogTerm = ParseHeader(RecordTermHeader, headers, Int64Parser);
        }

        internal RequestVoteMessage(HttpRequest request)
            : this(request.Headers.TryGetValue)
        {
        }

        internal override void PrepareRequest(HttpRequestMessage request)
        {
            request.Headers.Add(RecordIndexHeader, LastLogIndex.ToString(InvariantCulture));
            request.Headers.Add(RecordTermHeader, LastLogTerm.ToString(InvariantCulture));
            base.PrepareRequest(request);
        }

        Task<Result<bool>> IHttpMessageReader<Result<bool>>.ParseResponse(HttpResponseMessage response, CancellationToken token) => ParseBoolResponse(response, token);

        public new Task SaveResponse(HttpResponse response, Result<bool> result, CancellationToken token) => RaftHttpMessage.SaveResponse(response, result, token);
    }
}

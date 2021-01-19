using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class PreVoteMessage : RaftHttpMessage, IHttpMessageReader<Result<bool>>, IHttpMessageWriter<Result<bool>>
    {
        internal new const string MessageType = "PreVote";

        internal readonly long LastLogIndex;
        internal readonly long LastLogTerm;

        internal PreVoteMessage(ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm)
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

        Task<Result<bool>> IHttpMessageReader<Result<bool>>.ParseResponse(HttpResponseMessage response, CancellationToken token) => ParseBoolResponse(response, token);

        public new Task SaveResponse(HttpResponse response, Result<bool> result, CancellationToken token) => RaftHttpMessage.SaveResponse(response, result, token);
    }
}
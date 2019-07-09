using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class RequestVoteMessage : RaftHttpMessage, IHttpMessageReader<Result<bool>>, IHttpMessageWriter<Result<bool>>
    {
        private const string RecordIndexHeader = "X-Raft-Record-Index";
        internal const string RecordTermHeader = "X-Raft-Record-Term";
        internal new const string MessageType = "RequestVote";

        internal readonly long LastLogIndex;
        internal readonly long LastLogTerm;

        internal RequestVoteMessage(IPEndPoint sender, long term, long lastLogIndex, long lastLogTerm)
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

        private protected override void FillRequest(HttpRequestMessage request)
        {
            base.FillRequest(request);
            request.Headers.Add(RecordIndexHeader, Convert.ToString(LastLogIndex, InvariantCulture));
            request.Headers.Add(RecordTermHeader, Convert.ToString(LastLogTerm, InvariantCulture));
        }

        Task<Result<bool>> IHttpMessageReader<Result<bool>>.ParseResponse(HttpResponseMessage response) => ParseBoolResponse(response);

        public new Task SaveResponse(HttpResponse response, Result<bool> result) => RaftHttpMessage.SaveResponse(response, result);
    }
}

using Microsoft.AspNetCore.Http;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class RequestVoteMessage : RaftHttpMessage, IHttpMessageReader<bool>, IHttpMessageWriter<bool>
    {
        internal const string RecordIndexHeader = "X-Raft-Record-Index";
        internal const string RecordTermHeader = "X-Raft-Record-Term";
        internal new const string MessageType = "RequestVote";

        internal readonly LogEntryId? LastEntry;

        internal RequestVoteMessage(IPEndPoint sender, long term, LogEntryId? lastEntry)
            : base(MessageType, sender, term)
        {
            LastEntry = lastEntry;
        }

        internal RequestVoteMessage(HttpRequest request)
            : base(request)
        {
            LastEntry = ParseLogEntryId(request, RecordIndexHeader, RecordTermHeader);
        }

        Task<bool> IHttpMessageReader<bool>.ParseResponse(HttpResponseMessage response) => ParseBoolResponse(response);

        public new Task SaveResponse(HttpResponse response, bool result) => HttpMessage.SaveResponse(response, result);
    }
}

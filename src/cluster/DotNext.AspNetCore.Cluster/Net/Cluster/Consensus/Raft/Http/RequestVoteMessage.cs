using System.Net;
using DotNext.Net.Cluster.Replication;
using Microsoft.AspNetCore.Http;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class RequestVoteMessage : RaftHttpBooleanMessage
    {
        internal const string RecordIndexHeader = "X-Raft-Record-Index";
        internal const string RecordTermHeader = "X-Raft-Record-Term";
        internal new const string MessageType = "RequestVote";

        internal readonly LogEntryId? LastEntry;

        internal RequestVoteMessage(IPEndPoint sender, LogEntryId? lastEntry)
            : base(MessageType, sender)
        {
            LastEntry = lastEntry;
        }

        internal RequestVoteMessage(HttpRequest request)
            : base(request)
        {
            LastEntry = ParseLogEntryId(request, RecordIndexHeader, RecordTermHeader);
        }
    }
}

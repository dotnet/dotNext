using Microsoft.AspNetCore.Http;
using System;
using System.Net;
using System.Net.Http;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Replication;

    internal sealed class AppendEntriesMessage : RaftHttpBooleanMessage
    {
        internal new const string MessageType = "AppendEntries";
        private const string PrecedingRecordIndexHeader = "X-Raft-Preceding-Record-Index";
        private const string PrecedingRecordTermHeader = "X-Raft-Preceding-Record-Term";

        private sealed class MessageContent : OutboundMessageContent
        {
            internal MessageContent(ILogEntry<LogEntryId> message, LogEntryId precedingEntry)
                : base(message)
            {
                Headers.Add(PrecedingRecordIndexHeader, Convert.ToString(precedingEntry.Index, InvariantCulture));
                Headers.Add(PrecedingRecordTermHeader, Convert.ToString(precedingEntry.Term, InvariantCulture));
                Headers.Add(RequestVoteMessage.RecordIndexHeader, Convert.ToString(message.Id.Index, InvariantCulture));
                Headers.Add(RequestVoteMessage.RecordTermHeader, Convert.ToString(message.Id.Term, InvariantCulture));
            }
        }

        private sealed class ReceivedLogEntry : InboundMessageContent, ILogEntry<LogEntryId>
        {
            private readonly LogEntryId recordId;

            internal ReceivedLogEntry(HttpRequest request)
                : base(request)
            {
                recordId =
                    ParseLogEntryId(request, RequestVoteMessage.RecordIndexHeader,
                        RequestVoteMessage.RecordTermHeader) ??
                    throw new RaftProtocolException(
                        ExceptionMessages.MissingHeader(RequestVoteMessage.RecordIndexHeader));
            }

            ref readonly LogEntryId ILogEntry<LogEntryId>.Id => ref recordId;
        }

        internal readonly ILogEntry<LogEntryId> LogEntry;
        internal readonly LogEntryId PrecedingEntry;

        internal AppendEntriesMessage(IPEndPoint sender, ILogEntry<LogEntryId> entry, LogEntryId precedingEntry)
            : base(MessageType, sender)
        {
            LogEntry = entry;
            PrecedingEntry = precedingEntry;
        }

        internal AppendEntriesMessage(HttpRequest request)
            : base(request)
        {
            PrecedingEntry = ParseLogEntryId(request, PrecedingRecordIndexHeader, PrecedingRecordTermHeader) ??
                             throw new RaftProtocolException(
                                 ExceptionMessages.MissingHeader(PrecedingRecordIndexHeader));
            LogEntry = new ReceivedLogEntry(request);
        }



        private protected override void FillRequest(HttpRequestMessage request)
        {
            base.FillRequest(request);
            request.Content = new MessageContent(LogEntry, PrecedingEntry);
        }
    }
}
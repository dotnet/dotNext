using System;
using System.IO;
using System.Linq;
using static System.Globalization.CultureInfo;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using MediaTypeHeaderValue = System.Net.Http.Headers.MediaTypeHeaderValue;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Messaging;
    using Replication;

    internal sealed class AppendEntriesMessage : RaftHttpBooleanMessage
    {
        internal const string MessageType = "AppendEntries";
        private const string PrecedingRecordIndexHeader = "X-Raft-Preceding-Record-Index";
        private const string PrecedingRecordTermHeader = "X-Raft-Preceding-Record-Term";
        private const string RecordNameHeader = "X-Raft-Record-Name";

        private sealed class MessageContent : HttpContent
        {
            private readonly IMessage message;

            internal MessageContent(ILogEntry<LogEntryId> message, LogEntryId precedingEntry)
            {
                this.message = message;
                Headers.ContentType = MediaTypeHeaderValue.Parse(message.Type.ToString());
                Headers.Add(PrecedingRecordIndexHeader, Convert.ToString(precedingEntry.Index, InvariantCulture));
                Headers.Add(PrecedingRecordTermHeader, Convert.ToString(precedingEntry.Term, InvariantCulture));
                Headers.Add(RequestVoteMessage.RecordIndexHeader, Convert.ToString(message.Id.Index, InvariantCulture));
                Headers.Add(RequestVoteMessage.RecordTermHeader, Convert.ToString(message.Id.Term, InvariantCulture));
                Headers.Add(RecordNameHeader, message.Name);
            }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
                => message.CopyToAsync(stream);

            protected override bool TryComputeLength(out long length) => message.Length.TryGet(out length);
        }

        private sealed class ReceivedLogEntry : ILogEntry<LogEntryId>
        {
            private readonly LogEntryId recordId;
            private readonly ContentType contentType;
            private readonly long? length;
            private readonly string name;
            private readonly Stream content;

            internal ReceivedLogEntry(HttpRequest request)
            {
                length = request.ContentLength;
                recordId =
                    ParseLogEntryId(request, RequestVoteMessage.RecordIndexHeader,
                        RequestVoteMessage.RecordTermHeader) ??
                    throw new RaftProtocolException(
                        ExceptionMessages.MissingHeader(RequestVoteMessage.RecordIndexHeader));
                contentType = new ContentType(request.ContentType);
                name = request.Headers[RecordNameHeader].FirstOrDefault() ??
                       throw new RaftProtocolException(ExceptionMessages.MissingHeader(RecordNameHeader));
                content = request.Body;
            }

            string IMessage.Name => name;
            long? IMessage.Length => length;
            Task IMessage.CopyToAsync(Stream output) => content.CopyToAsync(output);

            ContentType IMessage.Type => contentType;

            ref readonly LogEntryId ILogEntry<LogEntryId>.Id => ref recordId;
        }

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

        internal ILogEntry<LogEntryId> LogEntry { get; }

        internal LogEntryId PrecedingEntry { get; }

        private protected override void FillRequest(HttpRequestMessage request)
        {
            base.FillRequest(request);
            request.Content = new MessageContent(LogEntry, PrecedingEntry);
        }
    }
}
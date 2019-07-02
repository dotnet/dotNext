using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Messaging;

    internal sealed class AppendEntriesMessage : RaftHttpMessage, IHttpMessageReader<Result<bool>>, IHttpMessageWriter<Result<bool>>
    {
        private const string MimeSubType = "mixed";
        private const string Boundary = "#######";
        internal new const string MessageType = "AppendEntries";
        private const string PrecedingRecordIndexHeader = "X-Raft-Preceding-Record-Index";
        private const string PrecedingRecordTermHeader = "X-Raft-Preceding-Record-Term";
        private const string CommitIndexHeader = "X-Raft-Commit-Index";

        private sealed class LogEntryContent : OutboundMessageContent
        {
            internal LogEntryContent(ILogEntry entry)
                : base(entry)
            {
                Headers.Add(RequestVoteMessage.RecordTermHeader, Convert.ToString(entry.Term, InvariantCulture));
            }
        }

        private sealed class ReceivedLogEntry : InboundMessageContent, ILogEntry
        {

            internal ReceivedLogEntry(MultipartSection section)
                : base(section)
            {
                Term = ParseHeader<StringValues, long>(RequestVoteMessage.RecordTermHeader, section.Headers.TryGetValue, Int64Parser);
            }

            public long Term { get; }
        }

        private IReadOnlyList<ILogEntry> entries;
        internal readonly long PrevLogIndex;
        internal readonly long PrevLogTerm;
        internal readonly long CommitIndex;

        internal AppendEntriesMessage(IPEndPoint sender, long term, long prevLogIndex, long prevLogTerm, long commitIndex)
            : base(MessageType, sender, term)
        {
            PrevLogIndex = prevLogIndex;
            PrevLogTerm = prevLogTerm;
            CommitIndex = commitIndex;
        }

        private AppendEntriesMessage(HeadersReader<StringValues> headers)
            : base(headers)
        {
            PrevLogIndex = ParseHeader(PrecedingRecordIndexHeader, headers, Int64Parser);
            PrevLogTerm = ParseHeader(PrecedingRecordTermHeader, headers, Int64Parser);
            CommitIndex = ParseHeader(CommitIndexHeader, headers, Int64Parser);
        }

        internal AppendEntriesMessage(HttpRequest request)
            : this(request.Headers.TryGetValue)
        {
        }

        internal IReadOnlyList<ILogEntry> Entries
        {
            get => entries ?? Array.Empty<ILogEntry>();
            set => entries = value;
        }

        internal async Task ParseEntriesAsync(HttpRequest request, CancellationToken token)
        {
            var reader = new MultipartReader(Boundary, request.Body);
            var entries = new List<ILogEntry>(10);
            while (true)
            {
                var section = await reader.ReadNextSectionAsync(token).ConfigureAwait(false);
                if (section is null)
                    break;
                entries.Add(new ReceivedLogEntry(section));
            }

            this.entries = entries;
        }

        private protected override void FillRequest(HttpRequestMessage request)
        {
            base.FillRequest(request);
            var content = new MultipartContent(MimeSubType, Boundary);
            foreach (var entry in Entries)
                content.Add(new LogEntryContent(entry));
            request.Content = content;
        }

        Task<Result<bool>> IHttpMessageReader<Result<bool>>.ParseResponse(HttpResponseMessage response) => ParseBoolResponse(response);

        public new Task SaveResponse(HttpResponse response, Result<bool> result) => RaftHttpMessage.SaveResponse(response, result);
    }
}
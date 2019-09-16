using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using static System.Globalization.CultureInfo;
using HeaderNames = Microsoft.Net.Http.Headers.HeaderNames;
using HeaderUtils = Microsoft.Net.Http.Headers.HeaderUtilities;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class AppendEntriesMessage : RaftHttpMessage, IHttpMessageReader<Result<bool>>, IHttpMessageWriter<Result<bool>>
    {
        private static readonly ValueParser<DateTimeOffset> DateTimeParser = (string str, out DateTimeOffset value) => HeaderUtils.TryParseDate(str, out value);

        private const int EntryBufferSize = 1024;
        private const string MimeSubType = "mixed";
        internal new const string MessageType = "AppendEntries";
        private const string PrecedingRecordIndexHeader = "X-Raft-Preceding-Record-Index";
        private const string PrecedingRecordTermHeader = "X-Raft-Preceding-Record-Term";
        private const string SnapshotRecordHeader = "X-Raft-Record-Snapshot";
        private const string CommitIndexHeader = "X-Raft-Commit-Index";

        private sealed class LogEntryContent : OutboundTransferObject
        {
            internal static readonly MediaTypeHeaderValue ContentType = MediaTypeHeaderValue.Parse(MediaTypeNames.Application.Octet);

            internal LogEntryContent(IRaftLogEntry entry)
                : base(entry)
            {
                Headers.ContentType = ContentType;
                Headers.Add(RequestVoteMessage.RecordTermHeader, entry.Term.ToString(InvariantCulture));
                Headers.Add(SnapshotRecordHeader, entry.IsSnapshot.ToString(InvariantCulture));
                Headers.LastModified = entry.Timestamp;
            }
        }

        private sealed class ReceivedLogEntry : StreamTransferObject, IRaftLogEntry
        {
            internal ReceivedLogEntry(MultipartSection section)
                : base(section.Body, true)
            {
                HeadersReader<StringValues> headers = section.Headers.TryGetValue;
                Term = ParseHeader(RequestVoteMessage.RecordTermHeader, headers, Int64Parser);
                IsSnapshot = ParseHeader(SnapshotRecordHeader, headers, BooleanParser);
                Timestamp = ParseHeader(HeaderNames.LastModified, headers, DateTimeParser);
            }

            public long Term { get; }

            public bool IsSnapshot { get; }

            public DateTimeOffset Timestamp { get; }
        }

        private IReadOnlyList<IRaftLogEntry> entries;
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

        internal IReadOnlyList<IRaftLogEntry> Entries
        {
            get => entries ?? Array.Empty<IRaftLogEntry>();
            set => entries = value;
        }

        internal async Task ParseEntriesAsync(HttpRequest request, CancellationToken token)
        {
            var boundary = request.GetMultipartBoundary();
            if (string.IsNullOrEmpty(boundary))
                this.entries = Array.Empty<IRaftLogEntry>();
            else
            {
                var reader = new MultipartReader(boundary, request.Body);
                var entries = new List<IRaftLogEntry>(10);
                while (true)
                {
                    var section = await reader.ReadNextSectionAsync(token).ConfigureAwait(false);
                    if (section is null)
                        break;
                    //assume that entry can be allocated in memory and doesn't require persistent buffer such as disk
                    var buffer = new MemoryStream(EntryBufferSize);
                    await section.Body.CopyToAsync(buffer).ConfigureAwait(false);
                    buffer.Seek(0, SeekOrigin.Begin);
                    section.Body = buffer;
                    entries.Add(new ReceivedLogEntry(section));
                }

                this.entries = entries;
            }
        }

        [SuppressMessage("Reliability", "CA2000", Justification = "Content of the log entry will be disposed automatically by ASP.NET infrastructure")]
        private protected override void FillRequest(HttpRequestMessage request)
        {
            base.FillRequest(request);
            request.Headers.Add(PrecedingRecordIndexHeader, PrevLogIndex.ToString(InvariantCulture));
            request.Headers.Add(PrecedingRecordTermHeader, PrevLogTerm.ToString(InvariantCulture));
            request.Headers.Add(CommitIndexHeader, CommitIndex.ToString(InvariantCulture));
            if (Entries.Count > 0)
            {
                var content = new MultipartContent(MimeSubType);
                foreach (var entry in Entries)
                    content.Add(new LogEntryContent(entry));
                request.Content = content;
            }
        }

        Task<Result<bool>> IHttpMessageReader<Result<bool>>.ParseResponse(HttpResponseMessage response, CancellationToken token) => ParseBoolResponse(response);

        public new Task SaveResponse(HttpResponse response, Result<bool> result, CancellationToken token) => RaftHttpMessage.SaveResponse(response, result, token);
    }
}
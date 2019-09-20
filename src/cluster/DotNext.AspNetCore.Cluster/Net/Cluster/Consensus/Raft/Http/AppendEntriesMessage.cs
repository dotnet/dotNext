using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using System;
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

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class AppendEntriesMessage : RaftHttpMessage, IHttpMessageReader<Result<bool>>, IHttpMessageWriter<Result<bool>>
    {
        private static readonly Func<ValueTask<IRaftLogEntry>> EmptyEnumerator = () => new ValueTask<IRaftLogEntry>(default(IRaftLogEntry));

        private const string MimeSubType = "mixed";
        internal new const string MessageType = "AppendEntries";
        private const string PrecedingRecordIndexHeader = "X-Raft-Preceding-Record-Index";
        private const string PrecedingRecordTermHeader = "X-Raft-Preceding-Record-Term";
        private const string CommitIndexHeader = "X-Raft-Commit-Index";

        private sealed class LogEntryContent : OutboundTransferObject
        {
            internal static readonly MediaTypeHeaderValue ContentType = MediaTypeHeaderValue.Parse(MediaTypeNames.Application.Octet);

            internal LogEntryContent(IRaftLogEntry entry)
                : base(entry)
            {
                Headers.ContentType = ContentType;
                Headers.Add(RequestVoteMessage.RecordTermHeader, entry.Term.ToString(InvariantCulture));
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
                Timestamp = ParseHeader(HeaderNames.LastModified, headers, DateTimeParser);
            }

            public long Term { get; }

            bool Replication.ILogEntry.IsSnapshot => false;

            public DateTimeOffset Timestamp { get; }
        }

        private sealed class ReceivedLogEntryReader : MultipartReader
        {
            internal ReceivedLogEntryReader(string boundary, Stream body)
                : base(boundary, body)
            {
            }

            internal async ValueTask<IRaftLogEntry> ParseEntryAsync()
            {
                var section = await ReadNextSectionAsync().ConfigureAwait(false);
                return section is null ? null : new ReceivedLogEntry(section);
            }
        }

        internal Func<ValueTask<IRaftLogEntry>> EntryReader;    //TODO: Should be replaced with IAsyncEnumerator in .NET Standard 2.1
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
            var boundary = request.GetMultipartBoundary();
            EntryReader = string.IsNullOrEmpty(boundary) ? EmptyEnumerator : new ReceivedLogEntryReader(boundary, request.Body).ParseEntryAsync;
        }

        [SuppressMessage("Reliability", "CA2000", Justification = "Content of the log entry will be disposed automatically by ASP.NET infrastructure")]
        internal override async ValueTask FillRequestAsync(HttpRequestMessage request)
        {
            request.Headers.Add(PrecedingRecordIndexHeader, PrevLogIndex.ToString(InvariantCulture));
            request.Headers.Add(PrecedingRecordTermHeader, PrevLogTerm.ToString(InvariantCulture));
            request.Headers.Add(CommitIndexHeader, CommitIndex.ToString(InvariantCulture));
            var isEmpty = true;
            var reader = EntryReader;
            if (reader is null)
                reader = EmptyEnumerator;
            var content = new MultipartContent(MimeSubType);
            IRaftLogEntry entry;
            while ((entry = await reader.Invoke().ConfigureAwait(false)) != null)
            {
                content.Add(new LogEntryContent(entry));
                isEmpty = false;
            }
            request.Content = isEmpty ? null : content;
            await base.FillRequestAsync(request).ConfigureAwait(false);
        }

        Task<Result<bool>> IHttpMessageReader<Result<bool>>.ParseResponse(HttpResponseMessage response, CancellationToken token) => ParseBoolResponse(response);

        public new Task SaveResponse(HttpResponse response, Result<bool> result, CancellationToken token) => RaftHttpMessage.SaveResponse(response, result, token);
    }
}
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Globalization.CultureInfo;
using HeaderNames = Microsoft.Net.Http.Headers.HeaderNames;
using HeaderUtils = Microsoft.Net.Http.Headers.HeaderUtilities;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Buffers;
    using Collections.Generic;
    using Replication;
    using static IO.StreamExtensions;
    using EncodingContext = Text.EncodingContext;

    internal class AppendEntriesMessage : RaftHttpMessage, IHttpMessageWriter<Result<bool>>
    {
        private static readonly ILogEntryProducer<ReceivedLogEntry> EmptyProducer = new LogEntryProducer<ReceivedLogEntry>();

        internal new const string MessageType = "AppendEntries";
        private const string PrecedingRecordIndexHeader = "X-Raft-Preceding-Record-Index";
        private const string PrecedingRecordTermHeader = "X-Raft-Preceding-Record-Term";
        private const string CommitIndexHeader = "X-Raft-Commit-Index";
        private protected const string CountHeader = "X-Raft-Entries-Count";

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

            bool ILogEntry.IsSnapshot => false;

            public DateTimeOffset Timestamp { get; }
        }

        private sealed class ReceivedLogEntryReader : MultipartReader, ILogEntryProducer<ReceivedLogEntry>
        {
            private long count;

            internal ReceivedLogEntryReader(string boundary, Stream body, long count) : base(boundary, body) => this.count = count;

            long ILogEntryProducer<ReceivedLogEntry>.RemainingCount => count;

            public ReceivedLogEntry Current
            {
                get;
                private set;
            }

            async ValueTask<bool> ILogEntryProducer<ReceivedLogEntry>.MoveNextAsync()
            {
                var section = await ReadNextSectionAsync().ConfigureAwait(false);
                if (section is null)
                    return false;
                Current = new ReceivedLogEntry(section);
                count -= 1L;
                return true;
            }
        }

        internal readonly long PrevLogIndex;
        internal readonly long PrevLogTerm;
        internal readonly long CommitIndex;

        private protected AppendEntriesMessage(IPEndPoint sender, long term, long prevLogIndex, long prevLogTerm, long commitIndex)
            : base(MessageType, sender, term)
        {
            PrevLogIndex = prevLogIndex;
            PrevLogTerm = prevLogTerm;
            CommitIndex = commitIndex;
        }

        private AppendEntriesMessage(HeadersReader<StringValues> headers, out long count)
            : base(headers)
        {
            PrevLogIndex = ParseHeader(PrecedingRecordIndexHeader, headers, Int64Parser);
            PrevLogTerm = ParseHeader(PrecedingRecordTermHeader, headers, Int64Parser);
            CommitIndex = ParseHeader(CommitIndexHeader, headers, Int64Parser);
            count = ParseHeader(CountHeader, headers, Int64Parser);
        }

        internal AppendEntriesMessage(HttpRequest request, out ILogEntryProducer<IRaftLogEntry> entries)
            : this(request.Headers.TryGetValue, out var count)
        {
            var boundary = request.GetMultipartBoundary();
            entries = string.IsNullOrEmpty(boundary) || count == 0L ? EmptyProducer : new ReceivedLogEntryReader(boundary, request.Body, count);
        }

        internal override void PrepareRequest(HttpRequestMessage request)
        {
            request.Headers.Add(PrecedingRecordIndexHeader, PrevLogIndex.ToString(InvariantCulture));
            request.Headers.Add(PrecedingRecordTermHeader, PrevLogTerm.ToString(InvariantCulture));
            request.Headers.Add(CommitIndexHeader, CommitIndex.ToString(InvariantCulture));
            base.PrepareRequest(request);
        }

        public new Task SaveResponse(HttpResponse response, Result<bool> result, CancellationToken token) => RaftHttpMessage.SaveResponse(response, result, token);
    }

    internal sealed class AppendEntriesMessage<TEntry, TList> : AppendEntriesMessage, IHttpMessageReader<Result<bool>>
        where TEntry : IRaftLogEntry
        where TList : IReadOnlyList<TEntry>
    {
        private static readonly Encoding DefaultHttpEncoding = Encoding.GetEncoding("iso-8859-1");

        /*
         * MultipartContent is not an option for this situation
         * Each log entry should not be boxed for allocated temporarily in the heap whenever possible
         * That's why stream-like writer of multipart content is here
         */
        private sealed class LogEntriesContent : HttpContent
        {
            private const string CrLf = "\r\n";
            private const string DoubleDash = "--";
            private const char Quote = '\"';
            private readonly Enumerable<TEntry, TList> entries;
            private readonly string boundary;

            internal LogEntriesContent(TList entries)
            {
                boundary = Guid.NewGuid().ToString();
                this.entries = new Enumerable<TEntry, TList>(entries);
                var contentType = new MediaTypeHeaderValue("multipart/mixed");
                contentType.Parameters.Add(new NameValueHeaderValue(nameof(boundary), Quote + boundary + Quote));
                Headers.ContentType = contentType;
            }

            internal int Count => entries.Count;

            private static void WriteHeader(StringBuilder builder, string headerName, string headerValue)
                => builder.Append(headerName).Append(": ").Append(headerValue).Append(CrLf);

            private static Task EncodeHeadersToStreamAsync(Stream output, StringBuilder builder, TEntry entry, bool writeDivider, string boundary, EncodingContext context, byte[] buffer)
            {
                builder.Clear();
                if (writeDivider)
                    builder.Append(CrLf + DoubleDash).Append(boundary).Append(CrLf);
                //write headers
                WriteHeader(builder, RequestVoteMessage.RecordTermHeader, entry.Term.ToString(InvariantCulture));
                WriteHeader(builder, HeaderNames.LastModified, HeaderUtils.FormatDate(entry.Timestamp));
                // Extra CRLF to end headers (even if there are no headers)
                builder.Append(CrLf);
                return output.WriteStringAsync(builder.ToString(), context, buffer);
            }

            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                const int maxChars = 128;   //it is empiric value measured using Console.WriteLine(builder.Length)
                EncodingContext encodingContext = DefaultHttpEncoding;
                using (var encodingBuffer = new ArrayRental<byte>(DefaultHttpEncoding.GetMaxByteCount(maxChars)))
                {
                    //write start boundary
                    await stream.WriteStringAsync(DoubleDash + boundary + CrLf, encodingContext, (byte[])encodingBuffer).ConfigureAwait(false);
                    encodingContext.Reset();
                    var builder = new StringBuilder(maxChars);
                    //write each nested content
                    var writeDivider = false;
                    foreach (var entry in entries)
                    {
                        await EncodeHeadersToStreamAsync(stream, builder, entry, writeDivider, boundary, encodingContext, (byte[])encodingBuffer).ConfigureAwait(false);
                        encodingContext.Reset();
                        Debug.Assert(builder.Length <= maxChars);
                        writeDivider = true;
                        await entry.CopyToAsync(stream).ConfigureAwait(false);
                    }
                    //write footer
                    await stream.WriteStringAsync(CrLf + DoubleDash + boundary + DoubleDash + CrLf, encodingContext, (byte[])encodingBuffer).ConfigureAwait(false);
                }
                encodingContext.Reset();
            }

            protected override bool TryComputeLength(out long length)
            {
                length = 0L;
                return false;
            }
        }

        private readonly TList entries;

        internal AppendEntriesMessage(IPEndPoint sender, long term, long prevLogIndex, long prevLogTerm, long commitIndex, TList entries)
            : base(sender, term, prevLogIndex, prevLogTerm, commitIndex)
            => this.entries = entries;

        internal override void PrepareRequest(HttpRequestMessage request)
        {
            request.Headers.Add(CountHeader, entries.Count.ToString(InvariantCulture));
            if (entries.Count > 0)
                request.Content = new LogEntriesContent(entries);
            base.PrepareRequest(request);
        }

        Task<Result<bool>> IHttpMessageReader<Result<bool>>.ParseResponse(HttpResponseMessage response, CancellationToken token) => ParseBoolResponse(response);
    }
}
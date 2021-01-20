using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using static System.Buffers.BuffersExtensions;
using static System.Globalization.CultureInfo;
using HeaderNames = Microsoft.Net.Http.Headers.HeaderNames;
using HeaderUtils = Microsoft.Net.Http.Headers.HeaderUtilities;
using MediaTypeNames = System.Net.Mime.MediaTypeNames;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Buffers;
    using Collections.Generic;
    using IO;
    using IO.Log;
    using static IO.Pipelines.PipeExtensions;
    using EncodingContext = Text.EncodingContext;

    internal class AppendEntriesMessage : RaftHttpMessage, IHttpMessageWriter<Result<bool>>
    {
        private static readonly ILogEntryProducer<MultipartLogEntry> EmptyProducer = new LogEntryProducer<MultipartLogEntry>();
        internal new const string MessageType = "AppendEntries";
        private const string PrecedingRecordIndexHeader = "X-Raft-Preceding-Record-Index";
        private const string PrecedingRecordTermHeader = "X-Raft-Preceding-Record-Term";
        private const string CommitIndexHeader = "X-Raft-Commit-Index";
        private protected const string CountHeader = "X-Raft-Entries-Count";

        private sealed class MultipartLogEntry : StreamTransferObject, IRaftLogEntry
        {
            internal MultipartLogEntry(MultipartSection section)
                : base(section.Body, true)
            {
                HeadersReader<StringValues> headers = GetHeaders(section).TryGetValue;
                Term = ParseHeader(RequestVoteMessage.RecordTermHeader, headers, Int64Parser);
                Timestamp = ParseHeader(HeaderNames.LastModified, headers, Rfc1123Parser);

                static IReadOnlyDictionary<string, StringValues> GetHeaders(MultipartSection section)
                {
                    IReadOnlyDictionary<string, StringValues>? headers = section.Headers;
                    return headers ?? ImmutableDictionary<string, StringValues>.Empty;
                }
            }

            public long Term { get; }

            bool ILogEntry.IsSnapshot => false;

            public DateTimeOffset Timestamp { get; }
        }

        private class OctetStreamLogEntry : IRaftLogEntry
        {
            private readonly PipeReader reader;
            private long length, timestamp, term;
            private bool consumed;

            private protected OctetStreamLogEntry(PipeReader reader)
            {
                this.reader = reader;
                consumed = true;
            }

            private protected bool Consumed => consumed;

            private protected ValueTask SkipAsync()
            {
                consumed = true;
                return reader.SkipAsync(length);
            }

            private protected async ValueTask ConsumeAsync()
            {
                // read term
                term = await reader.ReadInt64Async(true).ConfigureAwait(false);

                // read timestamp
                timestamp = await reader.ReadInt64Async(true).ConfigureAwait(false);

                // read length
                length = await reader.ReadInt64Async(true).ConfigureAwait(false);

                consumed = false;
            }

            long? IDataTransferObject.Length => length;

            DateTimeOffset ILogEntry.Timestamp => new DateTimeOffset(timestamp, TimeSpan.Zero);

            long IRaftLogEntry.Term => term;

            bool IDataTransferObject.IsReusable => false;

            bool ILogEntry.IsSnapshot => false;

            ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            {
                ValueTask result;
                if (consumed)
                {
                    result = new ValueTask(Task.FromException(new InvalidOperationException(ExceptionMessages.ReadLogEntryTwice)));
                }
                else
                {
                    consumed = true;
                    result = reader.ReadBlockAsync(length, writer, token);
                }

                return result;
            }
        }

        private sealed class MultipartLogEntriesReader : MultipartReader, ILogEntryProducer<MultipartLogEntry>, IDisposable
        {
            private long count;
            private MultipartLogEntry? current;

            internal MultipartLogEntriesReader(string boundary, Stream body, long count)
                : base(boundary, body) => this.count = count;

            long ILogEntryProducer<MultipartLogEntry>.RemainingCount => count;

            public MultipartLogEntry Current => current ?? throw new InvalidOperationException();

            async ValueTask<bool> IAsyncEnumerator<MultipartLogEntry>.MoveNextAsync()
            {
                if (current is not null)
                    await current.DisposeAsync().ConfigureAwait(false);
                var section = await ReadNextSectionAsync().ConfigureAwait(false);
                if (section is null)
                    return false;
                current = new MultipartLogEntry(section);
                count -= 1L;
                return true;
            }

            private void Dispose(bool disposing)
            {
                if (disposing)
                {
                    current?.Dispose();
                    current = null;
                }

                count = 0L;
            }

            void IDisposable.Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            ValueTask IAsyncDisposable.DisposeAsync()
            {
                var task = current?.DisposeAsync() ?? new ValueTask();
                current = null;
                count = 0L;
                GC.SuppressFinalize(this);
                return task;
            }

            ~MultipartLogEntriesReader() => Dispose(false);
        }

        // Reader and log entry is combined as a single class.
        // Such an approach allows to prevent allocation of objects for each log entry
        private sealed class OctetStreamLogEntriesReader : OctetStreamLogEntry, ILogEntryProducer<OctetStreamLogEntry>
        {
            private long count;

            internal OctetStreamLogEntriesReader(PipeReader reader, long count)
                : base(reader)
                => this.count = count;

            long ILogEntryProducer<OctetStreamLogEntry>.RemainingCount => count;

            OctetStreamLogEntry IAsyncEnumerator<OctetStreamLogEntry>.Current => this;

            async ValueTask<bool> IAsyncEnumerator<OctetStreamLogEntry>.MoveNextAsync()
            {
                if (count <= 0L)
                    return false;

                // Consumed == true if the previous log entry has been consumed completely
                // Consumed == false if the previous log entry was not consumed completely, only metadata
                if (!Consumed)
                    await SkipAsync().ConfigureAwait(false);

                await ConsumeAsync().ConfigureAwait(false);
                count -= 1L;
                return true;
            }

            ValueTask IAsyncDisposable.DisposeAsync()
            {
                count = 0L;
                return new ValueTask();
            }
        }

        internal readonly long PrevLogIndex;
        internal readonly long PrevLogTerm;
        internal readonly long CommitIndex;

        private protected AppendEntriesMessage(in ClusterMemberId sender, long term, long prevLogIndex, long prevLogTerm, long commitIndex)
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
            entries = CreateReader(request, count);
        }

        private static ILogEntryProducer<IRaftLogEntry> CreateReader(HttpRequest request, long count)
        {
            string boundary;

            if (count == 0L)
            {
                // jump to empty set of log entries
            }
            else if (request.ContentLength.HasValue)
            {
                // log entries encoded as efficient binary stream
                return new OctetStreamLogEntriesReader(request.BodyReader, count);
            }
            else if (!string.IsNullOrEmpty(boundary = request.GetMultipartBoundary()))
            {
                return new MultipartLogEntriesReader(boundary, request.Body, count);
            }

            return EmptyProducer;
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
        // This writer is used in situations when audit trail provides strong guarantees
        // that the IRaftLogEntry.Length is not null for any returned log entry.
        // If so, we can efficiently encode the series of log entries as binary stream with the
        // following format:
        // <term> - 8 bytes
        // <timestamp> - 8 bytes
        // <length> - 8 bytes
        // <payload> - octet string
        private sealed class OctetStreamLogEntriesWriter : HttpContent
        {
            private TList entries;

            internal OctetStreamLogEntriesWriter(in TList entries)
            {
                Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);
                this.entries = entries;
            }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
                => SerializeToStreamAsync(stream, context, CancellationToken.None);

#if NETCOREAPP3_1
            private
#else
            protected override
#endif
            async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken token)
            {
                using var buffer = new MemoryOwner<byte>(ArrayPool<byte>.Shared, 512);
                var writer = IAsyncBinaryWriter.Create(stream, buffer.Memory);
                foreach (var entry in entries)
                {
                    // write term
                    await writer.WriteInt64Async(entry.Term, true, token).ConfigureAwait(false);

                    // write timestamp
                    await writer.WriteInt64Async(entry.Timestamp.Ticks, true, token).ConfigureAwait(false);

                    // write length
                    await writer.WriteInt64Async(entry.Length.GetValueOrDefault(), true, token).ConfigureAwait(false);

                    // write log entry payload
                    await entry.WriteToAsync(writer, token).ConfigureAwait(false);
                }
            }

            protected override bool TryComputeLength(out long length)
            {
                length = 0L;
                foreach (var entry in entries)
                {
                    Debug.Assert(entry.Length.HasValue);

                    // sizeof(long) * 3 is a length of the log entry metadata
                    length += entry.Length.GetValueOrDefault() + (sizeof(long) * 3);
                }

                return true;
            }
        }

        /*
         * MultipartContent is not an option for this situation
         * Each log entry should not be boxed in the heap whenever possible
         * That's why stream-like writer of multipart content is here
         */
        private sealed class MultipartLogEntriesWriter : HttpContent
        {
            private const string ContentType = "multipart/mixed";
            private const string CrLf = "\r\n";
            private const string DoubleDash = "--";
            private const char Quote = '\"';
            private static readonly Encoding DefaultHttpEncoding = Encoding.GetEncoding("iso-8859-1");

            private readonly Enumerable<TEntry, TList> entries;
            private readonly string boundary;

            internal MultipartLogEntriesWriter(in TList entries)
            {
                boundary = Guid.NewGuid().ToString();
                this.entries = new Enumerable<TEntry, TList>(in entries);
                var contentType = new MediaTypeHeaderValue(ContentType);
                contentType.Parameters.Add(new NameValueHeaderValue(nameof(boundary), Quote + boundary + Quote));
                Headers.ContentType = contentType;
            }

            internal int Count => entries.Count;

            private static void WriteHeader(BufferWriter<char> builder, string headerName, string headerValue)
            {
                builder.Write(headerName);
                builder.Write(": ");
                builder.Write(headerValue);
                builder.Write(CrLf);
            }

            private static ValueTask EncodeHeadersToStreamAsync(Stream output, BufferWriter<char> builder, TEntry entry, bool writeDivider, string boundary, EncodingContext context, Memory<byte> buffer, CancellationToken token)
            {
                if (writeDivider)
                {
                    builder.Write(CrLf + DoubleDash);
                    builder.Write(boundary);
                    builder.Write(CrLf);
                }

                // write headers
                WriteHeader(builder, RequestVoteMessage.RecordTermHeader, entry.Term.ToString(InvariantCulture));
                WriteHeader(builder, HeaderNames.LastModified, HeaderUtils.FormatDate(entry.Timestamp));

                // Extra CRLF to end headers (even if there are no headers)
                builder.Write(CrLf);
                return output.WriteStringAsync(builder.WrittenMemory, context, buffer, token: token);
            }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
                => SerializeToStreamAsync(stream, context, CancellationToken.None);

#if NETCOREAPP3_1
            private
#else
            protected override
#endif
            async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken token)
            {
                const int maxChars = 128;   // it is empiric value measured using Console.WriteLine(builder.Length)
                EncodingContext encodingContext = DefaultHttpEncoding;
                using (var encodingBuffer = new MemoryOwner<byte>(ArrayPool<byte>.Shared, DefaultHttpEncoding.GetMaxByteCount(maxChars)))
                using (var builder = new PooledArrayBufferWriter<char>(maxChars))
                {
                    builder.Write(DoubleDash);
                    builder.Write(boundary);
                    builder.Write(CrLf);

                    // write start boundary
                    await stream.WriteStringAsync(builder.WrittenMemory, encodingContext, encodingBuffer.Memory, token: token).ConfigureAwait(false);
                    encodingContext.Reset();

                    // write each nested content
                    var writeDivider = false;
                    foreach (var entry in entries)
                    {
                        builder.Clear(true);
                        await EncodeHeadersToStreamAsync(stream, builder, entry, writeDivider, boundary, encodingContext, encodingBuffer.Memory, token).ConfigureAwait(false);
                        encodingContext.Reset();
                        Debug.Assert(builder.WrittenCount <= maxChars);
                        writeDivider = true;
                        await entry.WriteToAsync(stream, token: token).ConfigureAwait(false);
                    }

                    // write footer
                    builder.Clear(true);
                    builder.Write(CrLf + DoubleDash);
                    builder.Write(boundary);
                    builder.Write(DoubleDash + CrLf);
                    await stream.WriteStringAsync(builder.WrittenMemory, encodingContext, encodingBuffer.Memory, token: token).ConfigureAwait(false);
                }

                encodingContext.Reset();
            }

            protected override bool TryComputeLength(out long length)
            {
                length = 0L;
                return false;
            }
        }

        private TList entries;  // not readonly to avoid hidden copies
        private bool optimizedTransfer;

        internal AppendEntriesMessage(ClusterMemberId sender, long term, long prevLogIndex, long prevLogTerm, long commitIndex, TList entries)
            : base(sender, term, prevLogIndex, prevLogTerm, commitIndex)
            => this.entries = entries;

        internal bool UseOptimizedTransfer
        {
#if NETCOREAPP3_1
            set => optimizedTransfer = value;
#else
            init => optimizedTransfer = value;
#endif
        }

        internal override void PrepareRequest(HttpRequestMessage request)
        {
            request.Headers.Add(CountHeader, entries.Count.ToString(InvariantCulture));
            if (entries.Count > 0)
                request.Content = CreateContentProvider();
            base.PrepareRequest(request);
        }

        private HttpContent CreateContentProvider()
            => optimizedTransfer ? new OctetStreamLogEntriesWriter(in entries) : new MultipartLogEntriesWriter(in entries);

        Task<Result<bool>> IHttpMessageReader<Result<bool>>.ParseResponse(HttpResponseMessage response, CancellationToken token) => ParseBoolResponse(response, token);
    }
}
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using static System.Globalization.CultureInfo;
using AspNetMediaTypeHeaderValue = Microsoft.Net.Http.Headers.MediaTypeHeaderValue;
using HeaderUtils = Microsoft.Net.Http.Headers.HeaderUtilities;
using MediaTypeNames = System.Net.Mime.MediaTypeNames;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

using Buffers;
using IO;
using IO.Log;
using static IO.Pipelines.PipeExtensions;
using EncodingContext = Text.EncodingContext;
using LogEntryMetadata = NetworkTransport.LogEntryMetadata;

internal class AppendEntriesMessage : RaftHttpMessage, IHttpMessage
{
    internal const string MessageType = "AppendEntries";
    private const string PrecedingRecordIndexHeader = "X-Raft-Preceding-Record-Index";
    private const string PrecedingRecordTermHeader = "X-Raft-Preceding-Record-Term";
    private const string CommitIndexHeader = "X-Raft-Commit-Index";
    private protected const string CommandIdHeader = "X-Raft-Command-Id";
    private protected const string IsConfigurationHeader = "X-Raft-Configuration";
    private protected const string CountHeader = "X-Raft-Entries-Count";

    private sealed class MultipartLogEntry : StreamTransferObject, IRaftLogEntry
    {
        internal MultipartLogEntry(MultipartSection section)
            : base(section.Body, true)
        {
            Term = ParseHeader(section.Headers, RequestVoteMessage.RecordTermHeader, Int64Parser);
            CommandId = ParseHeaderAsNullable(section.Headers, CommandIdHeader, Int32Parser);
            IsConfiguration = ParseHeader(section.Headers, IsConfigurationHeader, BooleanParser);
        }

        public int? CommandId { get; }

        public long Term { get; }

        public bool IsConfiguration { get; }

        bool ILogEntry.IsSnapshot => false;
    }

    private class OctetStreamLogEntry : IRaftLogEntry
    {
        private readonly PipeReader reader;
        private Memory<byte> metadataBuffer;
        private LogEntryMetadata metadata;

        private protected OctetStreamLogEntry(PipeReader reader)
        {
            this.reader = reader;
            IsConsumed = true;
        }

        private protected bool IsConsumed { get; private set; }

        private protected ValueTask SkipAsync()
        {
            IsConsumed = true;
            return metadata.Length > 0L
                ? reader.SkipAsync(metadata.Length)
                : ValueTask.CompletedTask;
        }

        // fast path - attempt to consume metadata synchronously
        private bool TryConsume()
        {
            if (!reader.TryReadExactly(LogEntryMetadata.Size, out var result) || result.IsCanceled)
                return false;

            metadata = new(result.Buffer, out var metadataEnd);
            reader.AdvanceTo(metadataEnd);
            IsConsumed = false;
            return true;
        }

        // slow path - consume metadata asynchronously and allocate buffer on the heap
        private async ValueTask ConsumeSlowAsync()
        {
            if (metadataBuffer.IsEmpty)
                metadataBuffer = new byte[LogEntryMetadata.Size];

            await reader.ReadExactlyAsync(metadataBuffer).ConfigureAwait(false);
            metadata = new(metadataBuffer);
            IsConsumed = false;
        }

        private protected ValueTask ConsumeAsync()
            => TryConsume() ? ValueTask.CompletedTask : ConsumeSlowAsync();

        long? IDataTransferObject.Length => metadata.Length;

        long IRaftLogEntry.Term => metadata.Term;

        bool IDataTransferObject.IsReusable => false;

        bool ILogEntry.IsSnapshot => false;

        int? IRaftLogEntry.CommandId => metadata.CommandId;

        bool IRaftLogEntry.IsConfiguration => metadata.IsConfiguration;

        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            ValueTask result;
            if (IsConsumed)
            {
                result = ValueTask.FromException(new InvalidOperationException(ExceptionMessages.ReadLogEntryTwice));
            }
            else
            {
                IsConsumed = true;
                result = metadata.Length > 0L
                    ? reader.CopyToAsync(writer, metadata.Length, token)
                    : ValueTask.CompletedTask;
            }

            return result;
        }

        bool IDataTransferObject.TryGetMemory(out ReadOnlyMemory<byte> memory)
        {
            memory = ReadOnlyMemory<byte>.Empty;
            return metadata.Length is 0L;
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

            if (await ReadNextSectionAsync().ConfigureAwait(false) is not { } section)
                return false;

            current = new(section);
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
            if (!IsConsumed)
                await SkipAsync().ConfigureAwait(false);

            await ConsumeAsync().ConfigureAwait(false);
            count -= 1L;
            return true;
        }

        ValueTask IAsyncDisposable.DisposeAsync()
        {
            count = 0L;
            return new();
        }
    }

    internal readonly long PrevLogIndex;
    internal readonly long PrevLogTerm;
    internal readonly long CommitIndex;

    private protected AppendEntriesMessage(in ClusterMemberId sender, long term, long prevLogIndex, long prevLogTerm, long commitIndex)
        : base(sender, term)
    {
        PrevLogIndex = prevLogIndex;
        PrevLogTerm = prevLogTerm;
        CommitIndex = commitIndex;
    }

    private AppendEntriesMessage(IDictionary<string, StringValues> headers, out long entriesCount)
        : base(headers)
    {
        PrevLogIndex = ParseHeader(headers, PrecedingRecordIndexHeader, Int64Parser);
        PrevLogTerm = ParseHeader(headers, PrecedingRecordTermHeader, Int64Parser);
        CommitIndex = ParseHeader(headers, CommitIndexHeader, Int64Parser);
        entriesCount = ParseHeader(headers, CountHeader, Int64Parser);
    }

    internal AppendEntriesMessage(HttpRequest request, out ILogEntryProducer<IRaftLogEntry> entries)
        : this(request.Headers, out var entriesCount)
        => entries = CreateReader(request, entriesCount);

    private static ILogEntryProducer<IRaftLogEntry> CreateReader(HttpRequest request, long count)
    {
        var result = ILogEntryProducer<IRaftLogEntry>.Empty;

        if (count is 0L || !AspNetMediaTypeHeaderValue.TryParse(request.ContentType, out var mediaType))
        {
            // jump to empty set of log entries
        }
        else if (StringSegment.Equals(mediaType.MediaType, MediaTypeNames.Application.Octet, StringComparison.OrdinalIgnoreCase))
        {
            // log entries encoded as efficient binary stream
            result = new OctetStreamLogEntriesReader(request.BodyReader, count);
        }
        else if (HeaderUtils.RemoveQuotes(mediaType.Boundary) is { Length: > 0 } boundary)
        {
            result = new MultipartLogEntriesReader(boundary.ToString(), request.Body, count);
        }

        return result;
    }

    public new void PrepareRequest(HttpRequestMessage request)
    {
        request.Headers.Add(PrecedingRecordIndexHeader, PrevLogIndex.ToString(InvariantCulture));
        request.Headers.Add(PrecedingRecordTermHeader, PrevLogTerm.ToString(InvariantCulture));
        request.Headers.Add(CommitIndexHeader, CommitIndex.ToString(InvariantCulture));
        base.PrepareRequest(request);
    }

    static string IHttpMessage.MessageType => MessageType;

    internal static Task SaveResponseAsync(HttpResponse response, in Result<HeartbeatResult> result, CancellationToken token)
        => RaftHttpMessage.SaveResponseAsync(response, in result, token);
}

internal sealed class AppendEntriesMessage<TEntry, TList> : AppendEntriesMessage, IHttpMessage<Result<HeartbeatResult>>
    where TEntry : IRaftLogEntry
    where TList : IReadOnlyList<TEntry>
{
    // This writer is used in situations when audit trail provides strong guarantees
    // that the IRaftLogEntry.Length is not null for any returned log entry.
    // If so, we can efficiently encode the series of log entries as binary stream with the
    // following format:
    // <term> - 8 bytes
    // <timestamp> - 8 bytes
    // <flags> - 1 byte
    // <command-id> - 4 bytes
    // <length> - 8 bytes
    // <payload> - octet string
    private sealed class OctetStreamLogEntriesWriter : HttpContent
    {
        private TList entries; // not readonly to avoid defensive copies

        internal OctetStreamLogEntriesWriter(in TList entries)
        {
            Headers.ContentType = new(MediaTypeNames.Application.Octet);
            this.entries = entries;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken token)
        {
            using var buffer = new MemoryOwner<byte>(ArrayPool<byte>.Shared, 512);

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                // write metadata
                await WriteMetadataAsync(stream, buffer.Memory, entry, token).ConfigureAwait(false);

                // write log entry payload
                await entry.WriteToAsync(stream, buffer.Memory, token).ConfigureAwait(false);
            }
        }

        private static ValueTask WriteMetadataAsync(Stream output, Memory<byte> buffer, TEntry entry, CancellationToken token)
        {
            var metadata = LogEntryMetadata.Create(entry);
            buffer = buffer.Slice(0, LogEntryMetadata.Size);
            metadata.Format(buffer.Span);
            return output.WriteAsync(buffer, token);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0L;
            foreach (var entry in entries)
            {
                Debug.Assert(entry.Length.HasValue);
                checked
                {
                    length += entry.Length.GetValueOrDefault() + LogEntryMetadata.Size;
                }
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

        private readonly string boundary;
        private TList entries; // not readonly to avoid defensive copies

        internal MultipartLogEntriesWriter(in TList entries)
        {
            boundary = Guid.NewGuid().ToString();
            this.entries = entries;
            var contentType = new MediaTypeHeaderValue(ContentType);
            contentType.Parameters.Add(new(nameof(boundary), Quote + boundary + Quote));
            Headers.ContentType = contentType;
        }

        private static ValueTask<long> EncodeHeadersToStreamAsync(Stream output, BufferWriter<char> builder, TEntry entry, bool writeDivider, string boundary, EncodingContext context, Memory<byte> buffer, CancellationToken token)
        {
            if (writeDivider)
            {
                builder.Write(CrLf + DoubleDash);
                builder.Write(boundary);
                builder.Write(CrLf);
            }

            // write headers
            WriteHeader(builder, RequestVoteMessage.RecordTermHeader, entry.Term);
            WriteHeader<NullableFormattable<int>>(builder, CommandIdHeader, entry.CommandId);
            WriteHeader(builder, IsConfigurationHeader, entry.IsConfiguration);

            // Extra CRLF to end headers (even if there are no headers)
            builder.Write(CrLf);
            return output.EncodeAsync(builder.WrittenMemory, context, lengthFormat: null, buffer, token);
            
            static void WriteHeader<T>(BufferWriter<char> builder, ReadOnlySpan<char> headerName, T headerValue)
            {
                builder.Write(headerName);
                builder.Write(": ");
                builder.Format(headerValue, format: null, provider: InvariantCulture);
                builder.Write(CrLf);
            }
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken token)
        {
            const int maxChars = 256;   // it is empiric value measured using Console.WriteLine(builder.Length)
            var encodingContext = new EncodingContext(Encoding.Latin1, reuseEncoder: true);
            using var encodingBuffer = new MemoryOwner<byte>(ArrayPool<byte>.Shared, encodingContext.Encoding.GetMaxByteCount(maxChars));
            using var builder = new PoolingArrayBufferWriter<char> { Capacity = maxChars };

            // write
            builder.Write(DoubleDash);
            builder.Write(boundary);
            builder.Write(CrLf);

            // write start boundary
            await stream.EncodeAsync(builder.WrittenMemory, encodingContext, lengthFormat: null, encodingBuffer.Memory, token).ConfigureAwait(false);
            encodingContext.Reset();

            // write each nested content
            var writeDivider = false;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

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
            await stream.EncodeAsync(builder.WrittenMemory, encodingContext, lengthFormat: null, encodingBuffer.Memory, token).ConfigureAwait(false);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0L;
            return false;
        }
    }

    private TList entries;  // not readonly to avoid hidden copies

    internal AppendEntriesMessage(ClusterMemberId sender, long term, long prevLogIndex, long prevLogTerm, long commitIndex, TList entries)
        : base(sender, term, prevLogIndex, prevLogTerm, commitIndex)
    {
        this.entries = entries;
    }

    internal bool UseOptimizedTransfer { private get; init; }

    public new void PrepareRequest(HttpRequestMessage request)
    {
        request.Headers.Add(CountHeader, entries.Count.ToString(InvariantCulture));
        request.Content = CreateContentProvider();
        base.PrepareRequest(request);
    }

    private HttpContent CreateContentProvider()
        => UseOptimizedTransfer ? new OctetStreamLogEntriesWriter(in entries) : new MultipartLogEntriesWriter(in entries);

    Task<Result<HeartbeatResult>> IHttpMessage<Result<HeartbeatResult>>.ParseResponseAsync(HttpResponseMessage response, CancellationToken token)
        => ParseEnumResponseAsync<HeartbeatResult>(response, token);
}
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using static System.Buffers.BuffersExtensions;
using static System.Globalization.CultureInfo;
using AspNetMediaTypeHeaderValue = Microsoft.Net.Http.Headers.MediaTypeHeaderValue;
using HeaderNames = Microsoft.Net.Http.Headers.HeaderNames;
using HeaderUtils = Microsoft.Net.Http.Headers.HeaderUtilities;
using MediaTypeNames = System.Net.Mime.MediaTypeNames;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

using Buffers;
using Collections.Generic;
using IO;
using IO.Log;
using Membership;
using static IO.Pipelines.PipeExtensions;
using EncodingContext = Text.EncodingContext;
using LogEntryMetadata = TransportServices.LogEntryMetadata;

internal class AppendEntriesMessage : RaftHttpMessage, IHttpMessage
{
    private static readonly ILogEntryProducer<IRaftLogEntry> EmptyProducer = new LogEntryProducer<IRaftLogEntry>();
    internal const string MessageType = "AppendEntries";
    private const string PrecedingRecordIndexHeader = "X-Raft-Preceding-Record-Index";
    private const string PrecedingRecordTermHeader = "X-Raft-Preceding-Record-Term";
    private const string CommitIndexHeader = "X-Raft-Commit-Index";
    private protected const string CommandIdHeader = "X-Raft-Command-Id";
    private protected const string CountHeader = "X-Raft-Entries-Count";
    private protected const string ConfigurationLengthHeader = "X-Raft-Config-Length";
    private protected const string ConfigurationFingerprintHeader = "X-Raft-Config-Fingerprint";
    private protected const string ConfigurationCommitHeader = "X-Raft-Config-Commit";

    private sealed class MultipartLogEntry : StreamTransferObject, IRaftLogEntry
    {
        internal MultipartLogEntry(MultipartSection section)
            : base(section.Body, true)
        {
            Term = ParseHeader(section.Headers, RequestVoteMessage.RecordTermHeader, Int64Parser);
            Timestamp = ParseHeader(section.Headers, HeaderNames.LastModified, Rfc1123Parser);
            CommandId = ParseHeaderAsNullable(section.Headers, CommandIdHeader, Int32Parser);
        }

        public int? CommandId { get; }

        public long Term { get; }

        bool ILogEntry.IsSnapshot => false;

        public DateTimeOffset Timestamp { get; }
    }

    private class OctetStreamLogEntry : IRaftLogEntry
    {
        private readonly PipeReader reader;
        private Memory<byte> metadataBuffer;
        private LogEntryMetadata metadata;
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
            return metadata.Length > 0L ? reader.SkipAsync(metadata.Length) : new();
        }

        // fast path - attempt to consume metadata synchronously
        private bool TryConsume()
        {
            if (!reader.TryReadBlock(LogEntryMetadata.Size, out var result) || result.IsCanceled)
                return false;

            metadata = new(result.Buffer, out var metadataEnd);
            reader.AdvanceTo(metadataEnd);
            consumed = false;
            return true;
        }

        // slow path - consume metadata asynchronously and allocate buffer on the heap
        private async ValueTask ConsumeSlowAsync()
        {
            if (metadataBuffer.IsEmpty)
                metadataBuffer = new(new byte[LogEntryMetadata.Size]);

            await reader.ReadBlockAsync(metadataBuffer).ConfigureAwait(false);
            metadata = new(metadataBuffer);
            consumed = false;
        }

        private protected ValueTask ConsumeAsync() => TryConsume() ? new() : ConsumeSlowAsync();

        long? IDataTransferObject.Length => metadata.Length;

        DateTimeOffset ILogEntry.Timestamp => metadata.Timestamp;

        long IRaftLogEntry.Term => metadata.Term;

        bool IDataTransferObject.IsReusable => false;

        bool ILogEntry.IsSnapshot => false;

        int? IRaftLogEntry.CommandId => metadata.CommandId;

        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            ValueTask result;
            if (consumed)
            {
                result = ValueTask.FromException(new InvalidOperationException(ExceptionMessages.ReadLogEntryTwice));
            }
            else
            {
                consumed = true;
                result = metadata.Length > 0L ? reader.ReadBlockAsync(metadata.Length, writer, token) : new();
            }

            return result;
        }

        bool IDataTransferObject.TryGetMemory(out ReadOnlyMemory<byte> memory)
        {
            memory = ReadOnlyMemory<byte>.Empty;
            return metadata.Length == 0L;
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
            if (!Consumed)
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
    internal readonly long ConfigurationFingerprint;
    internal readonly long ConfigurationLength;
    internal readonly bool ApplyConfiguration;

    private protected AppendEntriesMessage(in ClusterMemberId sender, long term, long prevLogIndex, long prevLogTerm, long commitIndex, long fingerprint, long configurationLength, bool applyConfig)
        : base(sender, term)
    {
        PrevLogIndex = prevLogIndex;
        PrevLogTerm = prevLogTerm;
        CommitIndex = commitIndex;
        ConfigurationFingerprint = fingerprint;
        ConfigurationLength = configurationLength;
        ApplyConfiguration = applyConfig;
    }

    private AppendEntriesMessage(IDictionary<string, StringValues> headers, out long entriesCount)
        : base(headers)
    {
        PrevLogIndex = ParseHeader(headers, PrecedingRecordIndexHeader, Int64Parser);
        PrevLogTerm = ParseHeader(headers, PrecedingRecordTermHeader, Int64Parser);
        CommitIndex = ParseHeader(headers, CommitIndexHeader, Int64Parser);
        entriesCount = ParseHeader(headers, CountHeader, Int64Parser);
        ConfigurationFingerprint = ParseHeader(headers, ConfigurationFingerprintHeader, Int64Parser);
        ConfigurationLength = ParseHeader(headers, ConfigurationLengthHeader, Int64Parser);
        ApplyConfiguration = ParseHeader(headers, ConfigurationCommitHeader, BooleanParser);
    }

    internal AppendEntriesMessage(HttpRequest request, out Func<Memory<byte>, CancellationToken, ValueTask> configurationReader, out ILogEntryProducer<IRaftLogEntry> entries)
        : this(request.Headers, out var entriesCount)
    {
        entries = CreateReader(request, entriesCount);
        configurationReader = request.BodyReader.ReadBlockAsync;
    }

    private static ILogEntryProducer<IRaftLogEntry> CreateReader(HttpRequest request, long count)
    {
        var result = EmptyProducer;

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
        request.Headers.Add(ConfigurationFingerprintHeader, ConfigurationFingerprint.ToString(InvariantCulture));
        request.Headers.Add(ConfigurationLengthHeader, ConfigurationLength.ToString(InvariantCulture));
        request.Headers.Add(ConfigurationCommitHeader, ApplyConfiguration.ToString(InvariantCulture));
        base.PrepareRequest(request);
    }

    [RequiresPreviewFeatures]
    static string IHttpMessage.MessageType => MessageType;

    internal static Task SaveResponseAsync(HttpResponse response, Result<bool> result, CancellationToken token) => RaftHttpMessage.SaveResponseAsync(response, result, token);
}

internal sealed class AppendEntriesMessage<TEntry, TList> : AppendEntriesMessage, IHttpMessage<Result<bool>>
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
        private readonly IDataTransferObject configuration;
        private Enumerable<TEntry, TList> entries; // not readonly to avoid defensive copies

        internal OctetStreamLogEntriesWriter(in TList entries, IDataTransferObject configuration)
        {
            Headers.ContentType = new(MediaTypeNames.Application.Octet);
            this.configuration = configuration;
            this.entries = new(entries);
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken token)
        {
            using var buffer = new MemoryOwner<byte>(ArrayPool<byte>.Shared, 512);
            await configuration.WriteToAsync(stream, buffer.Memory, token).ConfigureAwait(false);

            foreach (var entry in entries)
            {
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
            metadata.Serialize(buffer.Span);
            return output.WriteAsync(buffer, token);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = configuration.Length.GetValueOrDefault();
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
        private readonly IDataTransferObject configuration;
        private Enumerable<TEntry, TList> entries; // not readonly to avoid defensive copies

        internal MultipartLogEntriesWriter(in TList entries, IDataTransferObject configuration)
        {
            boundary = Guid.NewGuid().ToString();
            this.entries = new(in entries);
            var contentType = new MediaTypeHeaderValue(ContentType);
            contentType.Parameters.Add(new(nameof(boundary), Quote + boundary + Quote));
            Headers.ContentType = contentType;
            this.configuration = configuration;
        }

        internal int Count => entries.Count;

        private static void WriteHeader(BufferWriter<char> builder, string headerName, string headerValue)
        {
            builder.Write(headerName);
            builder.Write(": ");
            builder.Write(headerValue);
            builder.Write(CrLf);
        }

        private static string GetCommandIdHeaderValue(int? id)
            => id.HasValue ? id.GetValueOrDefault().ToString(InvariantCulture) : string.Empty;

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
            WriteHeader(builder, CommandIdHeader, GetCommandIdHeaderValue(entry.CommandId));

            // Extra CRLF to end headers (even if there are no headers)
            builder.Write(CrLf);
            return output.WriteStringAsync(builder.WrittenMemory, context, buffer, token: token);
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken token)
        {
            const int maxChars = 256;   // it is empiric value measured using Console.WriteLine(builder.Length)
            var encodingContext = new EncodingContext(Encoding.Latin1, reuseEncoder: true);
            using (var encodingBuffer = new MemoryOwner<byte>(ArrayPool<byte>.Shared, encodingContext.Encoding.GetMaxByteCount(maxChars)))
            using (var builder = new PooledArrayBufferWriter<char> { Capacity = maxChars })
            {
                // encode configuration in raw format without boundaries
                await configuration.WriteToAsync(stream, encodingBuffer.Memory, token).ConfigureAwait(false);

                // write
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
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0L;
            return false;
        }
    }

    /*
     * Used only for transfer non-empty configuration and empty set of log entries
     */
    private sealed class ConfigurationWriter : HttpContent
    {
        private readonly IDataTransferObject configuration;

        internal ConfigurationWriter(IDataTransferObject configuration)
            => this.configuration = configuration;

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken token)
            => configuration.WriteToAsync(stream, token: token).AsTask();

        protected override bool TryComputeLength(out long length)
            => configuration.Length.TryGetValue(out length);
    }

    private readonly IDataTransferObject configuration;
    private TList entries;  // not readonly to avoid hidden copies

    internal AppendEntriesMessage(ClusterMemberId sender, long term, long prevLogIndex, long prevLogTerm, long commitIndex, TList entries, IClusterConfiguration configuration, bool applyConfig)
        : base(sender, term, prevLogIndex, prevLogTerm, commitIndex, configuration.Fingerprint, configuration.Length, applyConfig)
    {
        this.entries = entries;
        this.configuration = configuration;
    }

    internal bool UseOptimizedTransfer { private get; init; }

    public new void PrepareRequest(HttpRequestMessage request)
    {
        request.Headers.Add(CountHeader, entries.Count.ToString(InvariantCulture));
        request.Content = CreateContentProvider();
        base.PrepareRequest(request);
    }

    private HttpContent? CreateContentProvider()
    {
        if (entries.Count > 0)
            return UseOptimizedTransfer ? new OctetStreamLogEntriesWriter(in entries, configuration) : new MultipartLogEntriesWriter(in entries, configuration);

        return configuration.Length.GetValueOrDefault() > 0L ? new ConfigurationWriter(configuration) : null;
    }

    Task<Result<bool>> IHttpMessage<Result<bool>>.ParseResponseAsync(HttpResponseMessage response, CancellationToken token) => ParseBoolResponseAsync(response, token);
}
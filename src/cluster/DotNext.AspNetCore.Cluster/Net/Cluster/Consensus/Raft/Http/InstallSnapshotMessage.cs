using System.IO.Pipelines;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using static System.Globalization.CultureInfo;
using HeaderNames = Microsoft.Net.Http.Headers.HeaderNames;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

using IO;
using static IO.Pipelines.PipeExtensions;

internal sealed class InstallSnapshotMessage : RaftHttpMessage, IHttpMessageReader<Result<bool>>, IHttpMessageWriter<Result<bool>>
{
    internal new const string MessageType = "InstallSnapshot";
    private const string SnapshotIndexHeader = "X-Raft-Snapshot-Index";
    private const string SnapshotTermHeader = "X-Raft-Snapshot-Term";

    private sealed class ReceivedSnapshot : IRaftLogEntry
    {
        private readonly PipeReader reader;
        private bool touched;

        internal ReceivedSnapshot(PipeReader content) => reader = content;

        public long Term { get; init; }

        bool IO.Log.ILogEntry.IsSnapshot => true;

        public DateTimeOffset Timestamp { get; init; }

        public long? Length { get; init; }

        bool IDataTransferObject.IsReusable => false;

        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            ValueTask result;
            if (touched)
            {
                result = ValueTask.FromException(new InvalidOperationException(ExceptionMessages.ReadLogEntryTwice));
            }
            else
            {
                touched = true;
                result = new(reader.CopyToAsync(writer, token));
            }

            return result;
        }
    }

    private sealed class SnapshotContent : HttpContent
    {
        private readonly IDataTransferObject snapshot;

        internal SnapshotContent(IRaftLogEntry snapshot)
        {
            Headers.LastModified = snapshot.Timestamp;
            this.snapshot = snapshot;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken token)
            => snapshot.WriteToAsync(stream, token: token).AsTask();

        protected override bool TryComputeLength(out long length) => snapshot.Length.TryGetValue(out length);
    }

    internal readonly IRaftLogEntry Snapshot;
    internal readonly long Index;

    internal InstallSnapshotMessage(in ClusterMemberId sender, long term, long index, IRaftLogEntry snapshot)
        : base(MessageType, sender, term)
    {
        Index = index;
        Snapshot = snapshot;
    }

    private InstallSnapshotMessage(IDictionary<string, StringValues> headers, PipeReader body, long? length)
        : base(headers)
    {
        Index = ParseHeader(headers, SnapshotIndexHeader, Int64Parser);
        Snapshot = new ReceivedSnapshot(body)
        {
            Term = ParseHeader(headers, SnapshotTermHeader, Int64Parser),
            Timestamp = ParseHeader(headers, HeaderNames.LastModified, Rfc1123Parser),
            Length = length,
        };
    }

    internal InstallSnapshotMessage(HttpRequest request)
        : this(request.Headers, request.BodyReader, request.ContentLength)
    {
    }

    internal override void PrepareRequest(HttpRequestMessage request)
    {
        request.Headers.Add(SnapshotIndexHeader, Index.ToString(InvariantCulture));
        request.Headers.Add(SnapshotTermHeader, Snapshot.Term.ToString(InvariantCulture));
        request.Content = new SnapshotContent(Snapshot);
        base.PrepareRequest(request);
    }

    Task<Result<bool>> IHttpMessageReader<Result<bool>>.ParseResponse(HttpResponseMessage response, CancellationToken token) => ParseBoolResponse(response, token);

    public Task SaveResponse(HttpResponse response, Result<bool> result, CancellationToken token) => RaftHttpMessage.SaveResponse(response, result, token);
}
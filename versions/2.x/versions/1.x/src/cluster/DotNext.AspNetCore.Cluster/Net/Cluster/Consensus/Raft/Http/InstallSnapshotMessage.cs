using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static System.Globalization.CultureInfo;
using HeaderNames = Microsoft.Net.Http.Headers.HeaderNames;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class InstallSnapshotMessage : RaftHttpMessage, IHttpMessageReader<Result<bool>>, IHttpMessageWriter<Result<bool>>
    {
        internal new const string MessageType = "InstallSnapshot";
        private const string SnapshotIndexHeader = "X-Raft-Snapshot-Index";
        private const string SnapshotTermHeader = "X-Raft-Snapshot-Term";

        private sealed class ReceivedSnapshot : StreamTransferObject, IRaftLogEntry
        {
            internal ReceivedSnapshot(Stream content, long term, DateTimeOffset timestamp)
                : base(content, true)
            {
                Term = term;
                Timestamp = timestamp;
            }

            public long Term { get; }

            bool Replication.ILogEntry.IsSnapshot => true;

            public DateTimeOffset Timestamp { get; }
        }

        internal readonly IRaftLogEntry Snapshot;
        internal readonly long Index;

        internal InstallSnapshotMessage(IPEndPoint sender, long term, long index, IRaftLogEntry snapshot)
            : base(MessageType, sender, term)
        {
            Index = index;
            Snapshot = snapshot;
        }

        private InstallSnapshotMessage(HeadersReader<StringValues> headers, out long snapshotTerm, out DateTimeOffset timestamp)
            : base(headers)
        {
            Index = ParseHeader(SnapshotIndexHeader, headers, Int64Parser);
            snapshotTerm = ParseHeader(SnapshotTermHeader, headers, Int64Parser);
            timestamp = ParseHeader(HeaderNames.LastModified, headers, DateTimeParser);
        }

        internal InstallSnapshotMessage(HttpRequest request)
            : this(request.Headers.TryGetValue, out var snapshotTerm, out var timestamp)
        {
            Snapshot = new ReceivedSnapshot(request.Body, snapshotTerm, timestamp);
        }

        internal override void PrepareRequest(HttpRequestMessage request)
        {
            request.Headers.Add(SnapshotIndexHeader, Index.ToString(InvariantCulture));
            request.Headers.Add(SnapshotTermHeader, Snapshot.Term.ToString(InvariantCulture));
            request.Content = new OutboundTransferObject(Snapshot);
            request.Content.Headers.LastModified = Snapshot.Timestamp;
            base.PrepareRequest(request);
        }

        Task<Result<bool>> IHttpMessageReader<Result<bool>>.ParseResponse(HttpResponseMessage response, CancellationToken token) => ParseBoolResponse(response);

        public new Task SaveResponse(HttpResponse response, Result<bool> result, CancellationToken token) => RaftHttpMessage.SaveResponse(response, result, token);
    }
}

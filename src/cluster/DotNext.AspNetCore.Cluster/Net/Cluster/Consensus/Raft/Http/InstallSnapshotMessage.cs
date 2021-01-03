using System;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using static System.Globalization.CultureInfo;
using HeaderNames = Microsoft.Net.Http.Headers.HeaderNames;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
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

            internal ReceivedSnapshot(PipeReader content, long term, DateTimeOffset timestamp, long? length)
            {
                Term = term;
                Timestamp = timestamp;
                Length = length;
                reader = content;
            }

            public long Term { get; }

            bool IO.Log.ILogEntry.IsSnapshot => true;

            public DateTimeOffset Timestamp { get; }

            public long? Length { get; }

            bool IDataTransferObject.IsReusable => false;

            ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            {
                Task result;
                if (touched)
                {
                    result = Task.FromException(new InvalidOperationException(ExceptionMessages.ReadLogEntryTwice));
                }
                else
                {
                    result = reader.ReadAsync(WriteToAsync<TWriter>, writer, token);
                }

                return new ValueTask(result);
            }
        }

        internal readonly IRaftLogEntry Snapshot;
        internal readonly long Index;

        internal InstallSnapshotMessage(IPEndPoint sender, long term, long index, IRaftLogEntry snapshot)
            : base(MessageType, sender, term)
        {
            Index = index;
            Snapshot = snapshot;
        }

        private InstallSnapshotMessage(HeadersReader<StringValues> headers, PipeReader body, long? length)
            : base(headers)
        {
            Index = ParseHeader(SnapshotIndexHeader, headers, Int64Parser);
            Snapshot = new ReceivedSnapshot(body, ParseHeader(SnapshotTermHeader, headers, Int64Parser), ParseHeader(HeaderNames.LastModified, headers, Rfc1123Parser), length);
        }

        internal InstallSnapshotMessage(HttpRequest request)
            : this(request.Headers.TryGetValue, request.BodyReader, request.ContentLength)
        {
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

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
    using Buffers;
    using IO;
    using static IO.Pipelines.PipeExtensions;

    internal sealed class InstallSnapshotMessage : RaftHttpMessage, IHttpMessageReader<Result<bool>>, IHttpMessageWriter<Result<bool>>
    {
        internal new const string MessageType = "InstallSnapshot";
        private const string SnapshotIndexHeader = "X-Raft-Snapshot-Index";
        private const string SnapshotTermHeader = "X-Raft-Snapshot-Term";
        private const string ConfigurationHeader = "X-Raft-Config-Length";

        private sealed class ReceivedSnapshot : Disposable, IRaftLogEntry
        {
            private readonly PipeReader reader;
            private readonly long? totalLength;
            private readonly int? configurationLength;
            private MemoryOwner<byte> config;
            private bool touched;

            internal ReceivedSnapshot(PipeReader content, long term, DateTimeOffset timestamp, long? totalLength, int? configurationLength)
            {
                Term = term;
                Timestamp = timestamp;
                this.totalLength = totalLength;
                this.configurationLength = configurationLength;
                reader = content;
            }

            public long Term { get; }

            bool IO.Log.ILogEntry.IsSnapshot => true;

            internal ValueTask ReadConfigurationAsync(CancellationToken token)
            {
                ValueTask result;

                if (configurationLength is null)
                {
                    result = new();
                }
                else
                {
                    config = MemoryAllocator.Allocate<byte>(configurationLength.GetValueOrDefault(), true);
                    result = reader.ReadBlockAsync(config.Memory, token);
                }

                return result;
            }

            bool IRaftLogEntry.TryGetClusterConfiguration(out MemoryOwner<byte> configuration)
            {
                if (configurationLength is null)
                {
                    configuration = default;
                    return false;
                }

                configuration = Span.Copy<byte>(config.Memory.Span);
                return true;
            }

            public DateTimeOffset Timestamp { get; }

            public long? Length
                => configurationLength is null ? totalLength : totalLength.HasValue ? totalLength.GetValueOrDefault() - configurationLength.GetValueOrDefault() : null;

            bool IDataTransferObject.IsReusable => false;

            ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            {
                ValueTask result;
                if (touched)
                {
#if NETCOREAPP3_1
                    result = new (Task.FromException(new InvalidOperationException(ExceptionMessages.ReadLogEntryTwice)));
#else
                    result = ValueTask.FromException(new InvalidOperationException(ExceptionMessages.ReadLogEntryTwice));
#endif
                }
                else
                {
                    touched = true;
                    result = new(reader.CopyToAsync(writer, token));
                }

                return result;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    config.Dispose();

                base.Dispose(disposing);
            }
        }

        private sealed class SnapshotContent : HttpContent
        {
            private readonly IDataTransferObject snapshot;
            private MemoryOwner<byte> configuration;

            internal SnapshotContent(IRaftLogEntry snapshot)
            {
                Headers.LastModified = snapshot.Timestamp;
                if (snapshot.TryGetClusterConfiguration(out configuration))
                    Headers.Add(ConfigurationHeader, configuration.Length.ToString(InvariantCulture));

                this.snapshot = snapshot;
            }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
                => SerializeToStreamAsync(stream, context, CancellationToken.None);

#if NETCOREAPP3_1
            private
#else
            protected override
#endif
            Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken token)
                => configuration.IsEmpty ? SerializeSnapshotOnlyAsync(stream, token) : SerializeSnapshotAndConfigurationAsync(stream, token);

            private Task SerializeSnapshotOnlyAsync(Stream stream, CancellationToken token)
                => snapshot.WriteToAsync(stream, token: token).AsTask();

            private async Task SerializeSnapshotAndConfigurationAsync(Stream stream, CancellationToken token)
            {
                await stream.WriteAsync(configuration.Memory, token).ConfigureAwait(false);
                await snapshot.WriteToAsync(stream, token: token).ConfigureAwait(false);
            }

            protected override bool TryComputeLength(out long length)
            {
                if (snapshot.Length.TryGetValue(out length))
                {
                    length += configuration.Length;
                    return true;
                }

                length = default;
                return false;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    configuration.Dispose();

                base.Dispose(disposing);
            }
        }

        internal readonly IRaftLogEntry Snapshot;
        internal readonly long Index;

        internal InstallSnapshotMessage(in ClusterMemberId sender, long term, long index, IRaftLogEntry snapshot)
            : base(MessageType, sender, term)
        {
            Index = index;
            Snapshot = snapshot;
        }

        private InstallSnapshotMessage(HeadersReader<StringValues> headers, PipeReader body, long? length, out Func<CancellationToken, ValueTask> configurationReader)
            : base(headers)
        {
            Index = ParseHeader(SnapshotIndexHeader, headers, Int64Parser);
            var configurationLength = ParseHeaderAsNullable(ConfigurationHeader, headers, Int32Parser);
            var snapshot = new ReceivedSnapshot(body, ParseHeader(SnapshotTermHeader, headers, Int64Parser), ParseHeader(HeaderNames.LastModified, headers, Rfc1123Parser), length, configurationLength);
            configurationReader = snapshot.ReadConfigurationAsync;
            Snapshot = snapshot;
        }

        internal InstallSnapshotMessage(HttpRequest request, out Func<CancellationToken, ValueTask> configurationReader)
            : this(request.Headers.TryGetValue, request.BodyReader, request.ContentLength, out configurationReader)
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

        public new Task SaveResponse(HttpResponse response, Result<bool> result, CancellationToken token) => RaftHttpMessage.SaveResponse(response, result, token);
    }
}

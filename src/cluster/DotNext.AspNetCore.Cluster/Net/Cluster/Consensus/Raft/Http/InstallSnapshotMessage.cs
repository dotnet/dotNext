using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using DotNext.Buffers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

using IO;
using static IO.Pipelines.PipeExtensions;

internal sealed class InstallSnapshotMessage : RaftHttpMessage, IHttpMessage<Result<HeartbeatResult>>
{
    internal const string MessageType = "InstallSnapshot";
    private const string SnapshotIndexHeader = "X-Raft-Snapshot-Index";
    private const string SnapshotTermHeader = "X-Raft-Snapshot-Term";
    private const string ConfigurationLengthHeader = "X-Raft-Config-Length";
    private const string ConfigurationVersionHeader = "X-Raft-Config-Version";

    internal readonly long Index;
    internal readonly long ConfigurationVersion;
    internal readonly IRaftLogEntry Snapshot;
    internal readonly IDataTransferObject Configuration;
    
    public InstallSnapshotMessage(in ClusterMemberId sender, long term, IRaftLogEntry snapshot, long snapshotIndex,
        IDataTransferObject configuration, long configVersion)
        : base(sender, term)
    {
        Debug.Assert(configuration.Length is not null);
        
        Index = snapshotIndex;
        Configuration = configuration;
        ConfigurationVersion = configVersion;
        Snapshot = snapshot;
    }

    private InstallSnapshotMessage(IDictionary<string, StringValues> headers, PipeReader body, long? contentLength)
        : base(headers)
    {
        Index = ParseHeader(headers, SnapshotIndexHeader, Int64Parser);
        var configLength = ParseHeader(headers, ConfigurationLengthHeader, Int32Parser);
        ConfigurationVersion = ParseHeader(headers, ConfigurationVersionHeader, Int64Parser);
        Snapshot = new ReceivedSnapshot(body)
        {
            Term = ParseHeader(headers, SnapshotTermHeader, Int64Parser),
            Length = contentLength - configLength,
        };
        Configuration = new ReceivedConfiguration(body, configLength);
    }

    public InstallSnapshotMessage(HttpRequest request)
        : this(request.Headers, request.BodyReader, request.ContentLength)
    {
    }

    public ValueTask EnsureConfigurationConsumedAsync(CancellationToken token)
        => (Configuration as ReceivedConfiguration)?.SkipAsync(token) ?? ValueTask.CompletedTask;

    public new void PrepareRequest(HttpRequestMessage request)
    {
        request.Headers.Add(SnapshotIndexHeader, Index.ToString(InvariantCulture));
        request.Headers.Add(SnapshotTermHeader, Snapshot.Term.ToString(InvariantCulture));
        request.Headers.Add(ConfigurationLengthHeader, Configuration.Length.GetValueOrDefault().ToString(InvariantCulture));
        request.Headers.Add(ConfigurationVersionHeader, ConfigurationVersion.ToString(InvariantCulture));
        request.Content = new SnapshotAndConfigurationContent(Snapshot, Configuration);
        base.PrepareRequest(request);
    }

    static string IHttpMessage.MessageType => MessageType;
    
    Task<Result<HeartbeatResult>> IHttpMessage<Result<HeartbeatResult>>.ParseResponseAsync(HttpResponseMessage response, CancellationToken token)
        => ParseEnumResponseAsync<HeartbeatResult>(response, token);

    internal static Task SaveResponseAsync(HttpResponse response, in Result<HeartbeatResult> result, CancellationToken token)
        => RaftHttpMessage.SaveResponseAsync(response, result, token);
    
    private sealed class ReceivedSnapshot(PipeReader reader) : IRaftLogEntry
    {
        private PipeReader? reader = reader;

        public long Term { get; init; }

        bool IO.Log.ILogEntry.IsSnapshot => true;

        public long? Length { get; init; }

        bool IDataTransferObject.IsReusable => false;

        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            ValueTask result;
            if (reader is null)
            {
                result = ValueTask.FromException(new InvalidOperationException(ExceptionMessages.ReadLogEntryTwice));
            }
            else
            {
                var readerCopy = reader;
                reader = null;
                result = readerCopy.CopyToAsync(writer, token);
            }

            return result;
        }
    }
    
    private sealed class ReceivedConfiguration(PipeReader reader, long length) : IDataTransferObject
    {
        private PipeReader? reader = reader;
        
        long? IDataTransferObject.Length => length;
        
        bool IDataTransferObject.IsReusable => false;

        public ValueTask SkipAsync(CancellationToken token)
        {
            ValueTask task;
            if (reader is null)
            {
                task = ValueTask.CompletedTask;
            }
            else
            {
                task = reader.SkipAsync(length, token);
                reader = null;
            }

            return task;
        }
        
        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            ValueTask task;
            if (reader is null)
            {
                task = ValueTask.FromException(new InvalidOperationException(ExceptionMessages.ReadLogEntryTwice));
            }
            else
            {
                task = reader.CopyToAsync(writer, length, token);
                reader = null;
            }

            return task;
        }
    }
    
    private sealed class SnapshotAndConfigurationContent(IRaftLogEntry snapshot, IDataTransferObject configuration) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken token)
        {
            var buffer = MemoryAllocator<byte>.Default.AllocateAtLeast(1024);
            try
            {
                await configuration.WriteToAsync(stream, buffer.Memory, token).ConfigureAwait(false);
                await snapshot.WriteToAsync(stream, buffer.Memory, token).ConfigureAwait(false);
            }
            finally
            {
                buffer.Dispose();
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            if (snapshot.Length is { } snapshotLength && configuration.Length is { } configurationLength)
            {
                length = checked(snapshotLength + configurationLength);
                return true;
            }

            length = 0L;
            return false;
        }
    }
}
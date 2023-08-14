using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Buffers;
using IO;
using IO.Log;

internal partial class Server
{
    private sealed class ReceivedLogEntries : Disposable, ILogEntryProducer<ReceivedLogEntries>, IRaftLogEntry
    {
        internal readonly ClusterMemberId Id;
        internal readonly long Term, PrevLogIndex, PrevLogTerm, CommitIndex;
        internal readonly bool ApplyConfig;
        private readonly ProtocolStream stream;
        private readonly CancellationToken token;
        internal readonly InMemoryClusterConfiguration Configuration;
        private readonly MemoryAllocator<byte> allocator;
        private int entriesCount;
        private LogEntryMetadata metadata;
        private bool consumed;

        internal ReceivedLogEntries(ProtocolStream stream, MemoryAllocator<byte> allocator, CancellationToken token)
        {
            this.stream = stream;
            this.token = token;
            consumed = true;

            var reader = new SpanReader<byte>(stream.WrittenBufferSpan);
            (Id, Term, PrevLogIndex, PrevLogTerm, CommitIndex, entriesCount) = AppendEntriesMessage.Read(ref reader);
            ApplyConfig = ValueTypeExtensions.ToBoolean(reader.Read());

            var fingerprint = reader.ReadInt64(true);
            var configLength = reader.ReadInt64(true);

            Configuration = new(configLength > 0L ? allocator(checked((int)configLength)) : default, fingerprint);
            this.allocator = allocator;
        }

        public async ValueTask<bool> MoveNextAsync()
        {
            if (!consumed)
            {
                await stream.SkipAsync(token).ConfigureAwait(false);
                consumed = true;
            }

            bool result;
            if (result = entriesCount > 0)
            {
                // read metadata
                metadata = await ReadLogEntryMetadataAsync(stream, token).ConfigureAwait(false);
                stream.AdvanceReadCursor(LogEntryMetadata.Size);
                consumed = false;
                stream.ResetReadState();
                entriesCount--;
            }

            return result;
        }

        long ILogEntryProducer<ReceivedLogEntries>.RemainingCount => entriesCount;

        ReceivedLogEntries IAsyncEnumerator<ReceivedLogEntries>.Current => this;

        long? IDataTransferObject.Length => metadata.Length;

        bool IDataTransferObject.IsReusable => false;

        DateTimeOffset ILogEntry.Timestamp => metadata.Timestamp;

        int? IRaftLogEntry.CommandId => metadata.CommandId;

        long IRaftLogEntry.Term => metadata.Term;

        bool ILogEntry.IsSnapshot => metadata.IsSnapshot;

        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            Debug.Assert(!consumed);

            consumed = true;
            return new(writer.CopyFromAsync(stream, token));
        }

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        async ValueTask<TResult> IDataTransferObject.TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
        {
            // we don't need large buffer. It is used for encoding some special data types, such as strings
            using var buffer = allocator.Invoke(length: 512, exactSize: false);
            var result = await IDataTransferObject.TransformAsync<TResult, TTransformation>(stream, transformation, resetStream: false, buffer.Memory, token).ConfigureAwait(false);
            consumed = true;
            return result;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Configuration.Dispose();
            }

            base.Dispose(disposing);
        }

        ValueTask IAsyncDisposable.DisposeAsync() => DisposeAsync();

        private static async ValueTask<LogEntryMetadata> ReadLogEntryMetadataAsync(ProtocolStream protocol, CancellationToken token)
        {
            await protocol.ReadAsync(LogEntryMetadata.Size, token).ConfigureAwait(false);
            return Read(protocol.WrittenBufferSpan);

            static LogEntryMetadata Read(ReadOnlySpan<byte> responseData)
            {
                var reader = new SpanReader<byte>(responseData);
                return LogEntryMetadata.Parse(ref reader);
            }
        }
    }
}
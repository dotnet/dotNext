using System.Buffers;
using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using System.Runtime.InteropServices;
using Buffers;
using IO;
using IO.Log;

internal partial class Server
{
    private sealed class ReceivedLogEntries : MemoryManager<byte>, ILogEntryProducer<ReceivedLogEntries>, IRaftLogEntry
    {
        private readonly ProtocolStream stream;
        private readonly CancellationToken token;
        internal readonly InMemoryClusterConfiguration Configuration;
        private int entriesCount;
        private LogEntryMetadata metadata;
        private bool consumed;
        private Buffer buffer;
        private Buffer[]? pinnedBuffer;

        internal ReceivedLogEntries(ProtocolStream stream, MemoryAllocator<byte> allocator, out bool applyConfig, CancellationToken token)
        {
            this.stream = stream;
            this.token = token;
            consumed = true;

            var reader = new SpanReader<byte>(stream.WrittenBufferSpan);
            (buffer.Id, buffer.Term, buffer.PrevLogIndex, buffer.PrevLogTerm, buffer.CommitIndex, entriesCount) = reader.Read<AppendEntriesMessage>();
            applyConfig = Unsafe.BitCast<byte, bool>(reader.Read());

            var (fingerprint, configLength) = reader.Read<ConfigurationMessage>();

            Configuration = configLength > 0L
                ? new(allocator(int.CreateChecked(configLength)), fingerprint)
                : new(fingerprint);
        }

        internal ClusterMemberId Id => buffer.Id;

        internal long Term => buffer.Term;

        internal long PrevLogIndex => buffer.PrevLogIndex;

        internal long PrevLogTerm => buffer.PrevLogTerm;

        internal long CommitIndex => buffer.CommitIndex;

        public override Span<byte> GetSpan()
        {
            ref var buffer = ref pinnedBuffer is null
                ? ref this.buffer
                : ref MemoryMarshal.GetArrayDataReference(pinnedBuffer);

            return Span.AsBytes(ref buffer);
        }

        public override unsafe MemoryHandle Pin(int elementIndex = 0)
        {
            pinnedBuffer ??= GC.AllocateUninitializedArray<Buffer>(length: 1, pinned: true);
            return new(Unsafe.AsPointer(ref pinnedBuffer[elementIndex]));
        }

        public override void Unpin()
        {
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
                await stream.ReadAsync(LogEntryMetadata.Size, token).ConfigureAwait(false);
                metadata = new(stream.WrittenBufferSpan);
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
            return writer.CopyFromAsync(stream, count: null, token);
        }

        ValueTask<TResult> IDataTransferObject.TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
        {
            consumed = true;
            return IDataTransferObject.TransformAsync<TResult, TTransformation>(stream, transformation, resetStream: false, Memory, token);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Configuration.Dispose();
            }
        }

        ValueTask IAsyncDisposable.DisposeAsync()
        {
            var result = ValueTask.CompletedTask;
            try
            {
                Dispose(disposing: true);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
            finally
            {
                GC.SuppressFinalize(this);
            }

            return result;
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private struct Buffer
    {
        internal ClusterMemberId Id;
        internal long Term, PrevLogIndex, PrevLogTerm, CommitIndex;
    }
}
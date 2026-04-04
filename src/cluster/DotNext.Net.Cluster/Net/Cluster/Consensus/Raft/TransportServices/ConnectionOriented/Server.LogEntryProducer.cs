using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using IO;
using IO.Log;

internal partial class Server
{
    private sealed class ReceivedLogEntries : ILogEntryProducer<ReceivedLogEntries>, IRaftLogEntry
    {
        private readonly ProtocolStream stream;
        private readonly CancellationToken token;
        private int entriesCount;
        private LogEntryMetadata metadata;
        private bool consumed;
        private byte[]? buffer;

        public ReceivedLogEntries(ProtocolStream stream, int entriesCount, CancellationToken token)
        {
            Debug.Assert(entriesCount > 0);
            
            this.stream = stream;
            this.token = token;
            this.entriesCount = entriesCount;
            consumed = true;
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

        int? IRaftLogEntry.CommandId => metadata.CommandId;

        long IRaftLogEntry.Term => metadata.Term;

        bool ILogEntry.IsSnapshot => metadata.IsSnapshot;

        bool IRaftLogEntry.IsConfiguration => metadata.IsConfiguration;

        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            Debug.Assert(!consumed);

            consumed = true;
            return metadata.Length is { } length and <= int.MaxValue && stream.TryReadFrameData((int)length, out var payload)
                ? writer.Invoke(payload, token)
                : writer.CopyFromAsync(stream, count: null, token);
        }

        ValueTask<TResult> IDataTransferObject.TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
        {
            consumed = true;
            buffer ??= new byte[128];
            return IDataTransferObject.TransformAsync<TResult, TTransformation>(stream, transformation, resetStream: false, buffer, token);
        }

        ValueTask IAsyncDisposable.DisposeAsync() => ValueTask.CompletedTask;
    }
}
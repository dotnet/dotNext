using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using IO;
using IO.Log;

internal partial class ProtocolStream
{
    private sealed class LogEntryProducer : Disposable, ILogEntryProducer<IRaftLogEntry>, IRaftLogEntry
    {
        private readonly ProtocolStream stream;
        private readonly CancellationToken token;
        private int entriesCount;
        private LogEntryMetadata metadata;
        private bool consumed;

        internal LogEntryProducer(ProtocolStream stream, int entriesCount, CancellationToken token)
        {
            this.stream = stream;
            this.entriesCount = entriesCount;
            this.token = token;
            consumed = true;
        }

        async ValueTask<bool> IAsyncEnumerator<IRaftLogEntry>.MoveNextAsync()
        {
            if (entriesCount is 0)
                return false;

            if (!consumed)
                await stream.SkipAsync(token).ConfigureAwait(false);

            // read metadata
            metadata = await stream.ReadLogEntryMetadataAsync(token).ConfigureAwait(false);
            consumed = false;
            stream.readState = ReadState.FrameNotStarted;
            entriesCount -= 1;
            return true;
        }

        long ILogEntryProducer<IRaftLogEntry>.RemainingCount => entriesCount;

        IRaftLogEntry IAsyncEnumerator<IRaftLogEntry>.Current => this;

        long? IDataTransferObject.Length => metadata.Length;

        bool IDataTransferObject.IsReusable => false;

        DateTimeOffset ILogEntry.Timestamp => metadata.Timestamp;

        int? IRaftLogEntry.CommandId => metadata.CommandId;

        long IRaftLogEntry.Term => metadata.Term;

        bool ILogEntry.IsSnapshot => metadata.IsSnapshot;

        async ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            Debug.Assert(!consumed);

            await writer.CopyFromAsync(stream, token).ConfigureAwait(false);
            consumed = true;
        }

        ValueTask IAsyncDisposable.DisposeAsync() => DisposeAsync();
    }

    private unsafe ValueTask<LogEntryMetadata> ReadLogEntryMetadataAsync(CancellationToken token)
        => ReadAsync<LogEntryMetadata>(LogEntryMetadata.Size, &LogEntryMetadata.Parse, token);
}
namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using IO;
using IO.Log;

internal partial class ProtocolStream : IRaftLogEntry, ILogEntryProducer<IRaftLogEntry>
{
    private int entriesCount;
    private LogEntryMetadata metadata;
    private bool consumed;

    async ValueTask<bool> IAsyncEnumerator<IRaftLogEntry>.MoveNextAsync()
    {
        if (entriesCount is 0)
            return false;

        if (!consumed)
            await SkipAsync().ConfigureAwait(false);

        // read metadata
        metadata = await ReadLogEntryMetadataAsync(CancellationToken.None).ConfigureAwait(false);
        consumed = false;
        entriesCount -= 1;
        return true;
    }

    private unsafe ValueTask<LogEntryMetadata> ReadLogEntryMetadataAsync(CancellationToken token)
        => ReadAsync<LogEntryMetadata>(LogEntryMetadata.Size, &LogEntryMetadata.Parse, token);

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
        await writer.CopyFromAsync(this, token).ConfigureAwait(false);
        consumed = true;
    }

    ValueTask IAsyncDisposable.DisposeAsync() => ValueTask.CompletedTask;
}
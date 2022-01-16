using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Buffers;
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
            stream.frameSize = 0;
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

        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            Debug.Assert(!consumed);

            consumed = true;
            return new(writer.CopyFromAsync(stream, token));
        }

        [AsyncStateMachine(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        async ValueTask<TResult> IDataTransferObject.TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
        {
            using var buffer = stream.allocator.Invoke(stream.BufferLength, exactSize: false);
            var result = await IDataTransferObject.TransformAsync<TResult, TTransformation>(stream, transformation, resetStream: false, buffer.Memory, token).ConfigureAwait(false);
            consumed = true;
            return result;
        }

        ValueTask IAsyncDisposable.DisposeAsync() => DisposeAsync();
    }

    private unsafe ValueTask<LogEntryMetadata> ReadLogEntryMetadataAsync(CancellationToken token)
        => ReadAsync<LogEntryMetadata>(LogEntryMetadata.Size, &LogEntryMetadata.Parse, token);
}
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.Datagram;

using IO;
using IO.Log;
using static Runtime.Intrinsics;
using AsyncEventHub = Threading.AsyncEventHub;

[StructLayout(LayoutKind.Auto)]
internal readonly struct ReceivedLogEntry : IRaftLogEntry
{
    private readonly PipeReader reader;
    private readonly LogEntryMetadata metadata;

    internal ReceivedLogEntry(ref ReadOnlyMemory<byte> prologue, PipeReader reader)
    {
        var count = EntriesExchange.ParseLogEntryPrologue(prologue.Span, out metadata);
        prologue = prologue.Slice(count);
        this.reader = reader;
    }

    internal ReceivedLogEntry(ref ReadOnlyMemory<byte> announcement, PipeReader reader, out ClusterMemberId sender, out long term, out long snapshotIndex)
    {
        var count = SnapshotExchange.ParseAnnouncement(announcement.Span, out sender, out term, out snapshotIndex, out metadata);
        announcement = announcement.Slice(count);
        this.reader = reader;
    }

    long? IDataTransferObject.Length => metadata.Length;

    long IRaftLogEntry.Term => metadata.Term;

    int? IRaftLogEntry.CommandId => metadata.CommandId;

    DateTimeOffset ILogEntry.Timestamp => metadata.Timestamp;

    bool ILogEntry.IsSnapshot => metadata.IsSnapshot;

    bool IDataTransferObject.IsReusable => false;

    ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        => new(writer.CopyFromAsync(reader, token));

    ValueTask<TResult> IDataTransferObject.TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
        => IDataTransferObject.TransformAsync<TResult, TTransformation>(reader, transformation, token);
}

internal partial class ServerExchange : ILogEntryProducer<ReceivedLogEntry>
{
    private sealed class EntriesExchangeCoordinator : AsyncEventHub
    {
        internal EntriesExchangeCoordinator()
            : base(5)
        {
        }

        private static int GetIndex(State state) => state - State.AppendEntriesReceived;

        internal void Signal(State state) => ResetAndPulse(GetIndex(state));

        [SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1500", Justification = "False positive")]
        internal Task WaitAnyAsync(State state1, State state2, CancellationToken token = default)
            => WaitAnyAsync(stackalloc int[] { GetIndex(state1), GetIndex(state2) }, token);

        [SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1500", Justification = "False positive")]
        internal Task WaitAnyAsync(State state1, State state2, State state3, CancellationToken token = default)
            => WaitAnyAsync(stackalloc int[] { GetIndex(state1), GetIndex(state2), GetIndex(state3) }, token);
    }

    private EntriesExchangeCoordinator? entriesExchangeCoordinator;
    private int remainingCount, runnningIndex;
    private ReceivedLogEntry currentEntry;

    long ILogEntryProducer<ReceivedLogEntry>.RemainingCount => remainingCount;

    ReceivedLogEntry IAsyncEnumerator<ReceivedLogEntry>.Current => currentEntry;

    async ValueTask<bool> IAsyncEnumerator<ReceivedLogEntry>.MoveNextAsync()
    {
        Debug.Assert(entriesExchangeCoordinator is not null);

        // at the moment of this method call entire exchange can be in the following states:
        // log entry headers are obtained and entire log entry ready to read
        // log entry content is completely obtained
        // iteration was not started
        // no more entries to enumerate
        if (remainingCount <= 0)
        {
            // resume wait thread to finalize response
            entriesExchangeCoordinator.Signal(state = State.ReceivingEntriesFinished);
            return false;
        }

        // Insert barrier: informs writer that we are no longer interested in the currently receiving log entry.
        // Then, waits the writer to be completed
        await Reader.CompleteAsync().ConfigureAwait(false);
        await entriesExchangeCoordinator.WaitAnyAsync(State.EntryReceived, State.AppendEntriesReceived).ConfigureAwait(false);

        // complete writer only for the first call of MoveNextAsync()
        if (runnningIndex < 0)
            await Writer.CompleteAsync().ConfigureAwait(false);

        // informs that we are ready to receive a new log entry
        runnningIndex += 1;
        entriesExchangeCoordinator.Signal(state = State.ReadyToReceiveEntry);

        // waits for the log entry
        await entriesExchangeCoordinator.WaitAnyAsync(State.ReceivingEntry, State.EntryReceived).ConfigureAwait(false);
        remainingCount -= 1;
        return true;
    }

    private void BeginReceiveEntries(ReadOnlySpan<byte> announcement, CancellationToken token)
    {
        runnningIndex = -1;
        if (entriesExchangeCoordinator is null)
        {
            entriesExchangeCoordinator = new();
        }
        else
        {
            entriesExchangeCoordinator.Reset();
        }

        entriesExchangeCoordinator.Signal(state = State.AppendEntriesReceived);
        EntriesExchange.ParseAnnouncement(announcement, out var sender, out var term, out var prevLogIndex, out var prevLogTerm, out var commitIndex, out remainingCount, out var configState);

        task = server.AppendEntriesAsync(sender, term, this, prevLogIndex, prevLogTerm, commitIndex, configState?.Fingerprint, configState is { ApplyConfig: true }, token).AsTask();
    }

    private async ValueTask<bool> BeginReceiveEntry(ReadOnlyMemory<byte> prologue, bool completed, CancellationToken token)
    {
        Debug.Assert(entriesExchangeCoordinator is not null);

        currentEntry = new ReceivedLogEntry(ref prologue, Reader);
        var result = await Writer.WriteAsync(prologue, token).ConfigureAwait(false);
        if (result.IsCompleted || completed)
        {
            await Writer.CompleteAsync().ConfigureAwait(false);
            state = State.EntryReceived;
        }
        else
        {
            state = State.ReceivingEntry;
        }

        entriesExchangeCoordinator.Signal(state);

        return true;
    }

    private async ValueTask<bool> ReceivingEntry(ReadOnlyMemory<byte> content, bool completed, CancellationToken token)
    {
        Debug.Assert(entriesExchangeCoordinator is not null);

        if (content.IsEmpty)
        {
            completed = true;
        }
        else
        {
            var result = await Writer.WriteAsync(content, token).ConfigureAwait(false);
            completed |= result.IsCompleted;
        }

        if (completed)
        {
            await Writer.CompleteAsync().ConfigureAwait(false);
            entriesExchangeCoordinator.Signal(state = State.EntryReceived);
        }

        return true;
    }

    private async ValueTask<(PacketHeaders, int, bool)> TransmissionControl(Memory<byte> output, CancellationToken token)
    {
        Debug.Assert(entriesExchangeCoordinator is not null);

        MessageType responseType;
        int count;
        bool isContinueReceiving;
        var resultTask = Cast<Task<Result<bool>>>(task);
        var stateTask = entriesExchangeCoordinator.WaitAnyAsync(State.ReceivingEntriesFinished, State.ReadyToReceiveEntry, State.ReceivingEntry, token);

        // wait for result or state transition
        if (ReferenceEquals(resultTask, await Task.WhenAny(resultTask, stateTask).ConfigureAwait(false)))
        {
            // result obtained, finalize transmission
            task = null;
            state = State.ReceivingEntriesFinished;
            remainingCount = 0;
            count = Result.Write(output.Span, resultTask.Result);
            isContinueReceiving = false;
            responseType = MessageType.None;
        }
        else
        {
            // should be in sync with IsReadyToProcessAction
            switch (state)
            {
                case State.ReceivingEntriesFinished:
                    var result = await resultTask.ConfigureAwait(false);
                    count = Result.Write(output.Span, in result);
                    isContinueReceiving = false;
                    responseType = MessageType.None;
                    break;
                case State.ReadyToReceiveEntry:
                    ReusePipe(false);
                    count = EntriesExchange.CreateNextEntryResponse(output.Span, runnningIndex);
                    isContinueReceiving = true;
                    responseType = MessageType.NextEntry;
                    break;
                case State.ReceivingEntry:
                    count = 0;
                    isContinueReceiving = true;
                    responseType = MessageType.Continue;
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        return (new PacketHeaders(responseType, FlowControl.Ack), count, isContinueReceiving);
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        remainingCount = -1;
        GC.SuppressFinalize(this);
        return new();
    }
}
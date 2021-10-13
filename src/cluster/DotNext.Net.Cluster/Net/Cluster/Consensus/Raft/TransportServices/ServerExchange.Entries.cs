using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using IO;
using IO.Log;
using Threading;
using static Runtime.Intrinsics;

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
    private sealed class EntriesExchangeCoordinator : Disposable
    {
        private sealed class EventSource : TaskCompletionSource
        {
            public EventSource()
                : base(TaskCreationOptions.RunContinuationsAsynchronously)
            {
            }
        }

        private readonly ReaderWriterLockSlim rwLock;
        private (EventSource ReceiveEntriesFinished, EventSource ReadyToReceiveEntry, EventSource AppendEntriesReceived, EventSource ReceivingEntry, EventSource EntryReceived) sources;

        internal EntriesExchangeCoordinator()
        {
            sources.AsSpan().Initialize();
            rwLock = new(LockRecursionPolicy.NoRecursion);
        }

        private TaskCompletionSource this[State state] => state switch
        {
            State.ReceivingEntriesFinished => sources.ReceiveEntriesFinished,
            State.ReadyToReceiveEntry => sources.ReadyToReceiveEntry,
            State.AppendEntriesReceived => sources.AppendEntriesReceived,
            State.ReceivingEntry => sources.ReceivingEntry,
            State.EntryReceived => sources.EntryReceived,
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };

        internal void Signal(State state)
        {
            rwLock.EnterWriteLock();

            try
            {
                foreach (ref var source in sources.AsSpan())
                {
                    if (source.Task.IsCompleted)
                        source = new();
                }

                this[state].SetResult();
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        internal Task WaitAsync(State state)
        {
            rwLock.EnterReadLock();
            try
            {
                return this[state].Task;
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        internal Task WaitAnyAsync(ReadOnlySpan<State> states, CancellationToken token = default)
        {
            Task result;

            if (states.IsEmpty)
            {
                result = Task.CompletedTask;
                goto exit;
            }

            rwLock.EnterReadLock();
            try
            {
                switch (states.Length)
                {
                    case 1:
                        result = this[states[0]].Task;
                        break;
                    case 2:
                        result = Task.WhenAny(this[states[0]].Task, this[states[1]].Task);
                        break;
                    default:
                        var tasks = new Task[states.Length];
                        var index = 0;

                        foreach (var state in states)
                            tasks[index++] = this[state].Task;

                        result = Task.WhenAny(tasks);
                        break;
                }
            }
            finally
            {
                rwLock.ExitReadLock();
            }

        exit:
            return result.WaitAsync(token);
        }

        [SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1500", Justification = "False positive")]
        internal Task WaitAnyAsync(State state1, State state2, CancellationToken token = default)
            => WaitAnyAsync(stackalloc State[] { state1, state2 }, token);

        [SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1500", Justification = "False positive")]
        internal Task WaitAnyAsync(State state1, State state2, State state3, CancellationToken token = default)
            => WaitAnyAsync(stackalloc State[] { state1, state2, state3 }, token);

        internal void CancelSuspendedCallers()
        {
            rwLock.EnterWriteLock();
            try
            {
                foreach (ref var source in sources.AsSpan())
                {
                    source.TrySetCanceled();
                    source = new();
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var source in sources.AsSpan())
                    TrySetDisposedException(source);

                rwLock.Dispose();
            }

            base.Dispose(disposing);
        }
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
        entriesExchangeCoordinator = new();
        entriesExchangeCoordinator.Signal(state = State.AppendEntriesReceived);
        EntriesExchange.ParseAnnouncement(announcement, out var sender, out var term, out var prevLogIndex, out var prevLogTerm, out var commitIndex, out remainingCount, out var configState);

        task = server.AppendEntriesAsync(sender, term, this, prevLogIndex, prevLogTerm, commitIndex, configState?.Fingerprint, configState?.ApplyConfig ?? false, token);
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
            count = IExchange.WriteResult(resultTask.Result, output.Span);
            isContinueReceiving = false;
            responseType = MessageType.None;
        }
        else
        {
            // should be in sync with IsReadyToProcessAction
            switch (state)
            {
                case State.ReceivingEntriesFinished:
                    count = IExchange.WriteResult(await resultTask.ConfigureAwait(false), output.Span);
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
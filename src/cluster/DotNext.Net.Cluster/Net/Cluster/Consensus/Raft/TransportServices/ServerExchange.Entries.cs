using System.IO.Pipelines;
using System.Runtime.InteropServices;
using static System.Threading.Timeout;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
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
        private sealed class ReadyToReceiveTransition : AsyncTrigger<StateHolder>.ITransition
        {
            internal static readonly ReadyToReceiveTransition Instance = new();

            private ReadyToReceiveTransition()
            {
            }

            bool AsyncTrigger<StateHolder>.ITransition.Test(StateHolder state)
                => state.Value is State.ReceivingEntry or State.EntryReceived or State.AppendEntriesReceived;

            void AsyncTrigger<StateHolder>.ITransition.Transit(StateHolder state)
                => state.Value = State.ReadyToReceiveEntry;
        }

        private sealed class ReadyToReadTransition : AsyncTrigger<StateHolder>.ITransition
        {
            internal static readonly ReadyToReadTransition Instance = new();

            private ReadyToReadTransition()
            {
            }

            bool AsyncTrigger<StateHolder>.ITransition.Test(StateHolder state)
                => state.Value is State.ReceivingEntry or State.EntryReceived;

            void AsyncTrigger<StateHolder>.ITransition.Transit(StateHolder state)
            {
                // do nothing here
            }
        }

        private sealed class ReadyToProcessTransition : AsyncTrigger<StateHolder>.ITransition
        {
            internal static readonly ReadyToProcessTransition Instance = new();

            private ReadyToProcessTransition()
            {
            }

            bool AsyncTrigger<StateHolder>.ITransition.Test(StateHolder state)
                => state.Value is State.ReceivingEntriesFinished or State.ReadyToReceiveEntry or State.ReceivingEntry;

            void AsyncTrigger<StateHolder>.ITransition.Transit(StateHolder state)
            {
                // do nothing here
            }
        }

        private static readonly Action<StateHolder, State> SetStateAction;

        static ServerExchange()
        {
            SetStateAction = SetState;

            static void SetState(StateHolder holder, State state) => holder.Value = state;
        }

        private readonly AsyncTrigger<StateHolder> transmissionStateTrigger;
        private int remainingCount, lookupIndex;
        private ReceivedLogEntry currentEntry;

        long ILogEntryProducer<ReceivedLogEntry>.RemainingCount => remainingCount;

        ReceivedLogEntry IAsyncEnumerator<ReceivedLogEntry>.Current => currentEntry;

        async ValueTask<bool> IAsyncEnumerator<ReceivedLogEntry>.MoveNextAsync()
        {
            // at the moment of this method call entire exchange can be in the following states:
            // log entry headers are obtained and entire log entry ready to read
            // log entry content is completely obtained
            // iteration was not started
            // no more entries to enumerate
            if (remainingCount <= 0)
            {
                // resume wait thread to finalize response
                transmissionStateTrigger.Signal(SetStateAction, State.ReceivingEntriesFinished);
                return false;
            }

            // informs that we are ready to receive a new log entry
            await Reader.CompleteAsync().ConfigureAwait(false);
            await transmissionStateTrigger.WaitAsync(ReadyToReceiveTransition.Instance).ConfigureAwait(false);

            // waits for the log entry
            await transmissionStateTrigger.WaitAsync(ReadyToReadTransition.Instance).ConfigureAwait(false);
            remainingCount -= 1;
            return true;
        }

        private void BeginReceiveEntries(ReadOnlySpan<byte> announcement, CancellationToken token)
        {
            lookupIndex = -1;
            CurrentState = State.AppendEntriesReceived;
            EntriesExchange.ParseAnnouncement(announcement, out var sender, out var term, out var prevLogIndex, out var prevLogTerm, out var commitIndex, out remainingCount, out var configState);

            task = server.AppendEntriesAsync(sender, term, this, prevLogIndex, prevLogTerm, commitIndex, configState?.Fingerprint, configState?.ApplyConfig ?? false, token);
        }

        private async ValueTask<bool> BeginReceiveEntry(ReadOnlyMemory<byte> prologue, bool completed, CancellationToken token)
        {
            currentEntry = new ReceivedLogEntry(ref prologue, Reader);
            var result = await Writer.WriteAsync(prologue, token).ConfigureAwait(false);
            if (result.IsCompleted | completed)
            {
                await Writer.CompleteAsync().ConfigureAwait(false);
                transmissionStateTrigger.Signal(SetStateAction, State.EntryReceived);
            }
            else
            {
                transmissionStateTrigger.Signal(SetStateAction, State.ReceivingEntry);
            }

            return true;
        }

        private async ValueTask<bool> ReceivingEntry(ReadOnlyMemory<byte> content, bool completed, CancellationToken token)
        {
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
                transmissionStateTrigger.Signal(SetStateAction, State.AppendEntriesReceived);
            }

            return true;
        }

        private async ValueTask<(PacketHeaders, int, bool)> TransmissionControl(Memory<byte> output, CancellationToken token)
        {
            MessageType responseType;
            int count;
            bool isContinueReceiving;
            var resultTask = Cast<Task<Result<bool>>>(task);
            var stateTask = transmissionStateTrigger.WaitAsync(ReadyToProcessTransition.Instance, token).AsTask();

            // wait for result or state transition
            if (ReferenceEquals(resultTask, await Task.WhenAny(resultTask, stateTask).ConfigureAwait(false)))
            {
                // result obtained, finalize transmission
                task = null;
                CurrentState = State.ReceivingEntriesFinished;
                remainingCount = 0;
                count = IExchange.WriteResult(resultTask.Result, output.Span);
                isContinueReceiving = false;
                responseType = MessageType.None;
            }
            else
            {
                // should be in sync with IsValidStateForResponse
                switch (CurrentState)
                {
                    case State.ReceivingEntriesFinished:
                        count = IExchange.WriteResult(await resultTask.ConfigureAwait(false), output.Span);
                        isContinueReceiving = false;
                        responseType = MessageType.None;
                        break;
                    case State.ReadyToReceiveEntry:
                        ReusePipe(false);
                        count = EntriesExchange.CreateNextEntryResponse(output.Span, lookupIndex);
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
}
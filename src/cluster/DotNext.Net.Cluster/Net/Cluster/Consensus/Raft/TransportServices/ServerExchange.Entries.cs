using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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

        internal ReceivedLogEntry(ref ReadOnlyMemory<byte> announcement, PipeReader reader, out ushort remotePort, out long term, out long snapshotIndex)
        {
            var count = SnapshotExchange.ParseAnnouncement(announcement.Span, out remotePort, out term, out snapshotIndex, out metadata);
            announcement = announcement.Slice(count);
            this.reader = reader;
        }

        long? IDataTransferObject.Length => metadata.Length;

        long IRaftLogEntry.Term => metadata.Term;

        DateTimeOffset ILogEntry.Timestamp => metadata.Timestamp;

        bool ILogEntry.IsSnapshot => metadata.IsSnapshot;

        bool IDataTransferObject.IsReusable => false;

        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => new ValueTask(writer.CopyFromAsync(reader, token));

        ValueTask<TResult> IDataTransferObject.TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
            => IDataTransferObject.TransformAsync<TResult, TTransformation>(reader, transformation, token);
    }

    internal partial class ServerExchange : ILogEntryProducer<ReceivedLogEntry>
    {
        private static readonly Action<ServerExchange, State> SetStateAction;
        private static readonly Predicate<ServerExchange> IsReadyToReadEntryPredicate, IsValidStateForResponsePredicate, IsValidForTransitionPredicate;

        static ServerExchange()
        {
            IsReadyToReadEntryPredicate = IsReadyToReadEntry;
            IsValidStateForResponsePredicate = IsValidStateForResponse;
            IsValidForTransitionPredicate = IsValidForTransition;
            SetStateAction = SetState;

            static bool IsReadyToReadEntry(ServerExchange server) => server.IsReadyToReadEntry();
            static bool IsValidStateForResponse(ServerExchange server) => server.IsValidStateForResponse();
            static bool IsValidForTransition(ServerExchange server) => server.IsValidForTransition();
            static void SetState(ServerExchange server, State state) => server.SetState(state);
        }

        private readonly AsyncTrigger transmissionStateTrigger = new AsyncTrigger();
        private int remainingCount, lookupIndex;
        private ReceivedLogEntry currentEntry;

        long ILogEntryProducer<ReceivedLogEntry>.RemainingCount => remainingCount;

        ReceivedLogEntry IAsyncEnumerator<ReceivedLogEntry>.Current => currentEntry;

        private void SetState(State newState) => state = newState;

        private bool IsReadyToReadEntry() => state.IsOneOf(State.ReceivingEntry, State.EntryReceived);

        private bool IsValidStateForResponse()
            => state.IsOneOf(State.ReceivingEntriesFinished, State.ReadyToReceiveEntry, State.ReceivingEntry);

        private bool IsValidForTransition() => state.IsOneOf(State.AppendEntriesReceived, State.EntryReceived);

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
                transmissionStateTrigger.Signal(this, SetStateAction, State.ReceivingEntriesFinished);
                return false;
            }

            if (lookupIndex >= 0)
            {
                await Reader.CompleteAsync().ConfigureAwait(false);
                await transmissionStateTrigger.WaitAsync(this, IsValidForTransitionPredicate).ConfigureAwait(false);
                ReusePipe(false);
            }

            lookupIndex += 1;
            remainingCount -= 1;

            return await transmissionStateTrigger.SignalAndWaitAsync(this, SetStateAction, State.ReadyToReceiveEntry, IsReadyToReadEntryPredicate, InfiniteTimeSpan).ConfigureAwait(false);
        }

        private void BeginReceiveEntries(EndPoint sender, ReadOnlySpan<byte> announcement, CancellationToken token)
        {
            lookupIndex = -1;
            state = State.AppendEntriesReceived;
            EntriesExchange.ParseAnnouncement(announcement, out var remotePort, out var term, out var prevLogIndex, out var prevLogTerm, out var commitIndex, out remainingCount);
            ChangePort(ref sender, remotePort);
            task = server.ReceiveEntriesAsync(sender, term, this, prevLogIndex, prevLogTerm, commitIndex, token);
        }

        private async ValueTask<bool> BeginReceiveEntry(ReadOnlyMemory<byte> prologue, bool completed, CancellationToken token)
        {
            currentEntry = new ReceivedLogEntry(ref prologue, Reader);
            var result = await Writer.WriteAsync(prologue, token).ConfigureAwait(false);
            if (result.IsCompleted | completed)
            {
                await Writer.CompleteAsync().ConfigureAwait(false);
                transmissionStateTrigger.Signal(this, SetStateAction, State.EntryReceived);
            }
            else
            {
                transmissionStateTrigger.Signal(this, SetStateAction, State.ReceivingEntry);
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
                transmissionStateTrigger.Signal(this, SetStateAction, State.AppendEntriesReceived);
            }

            return true;
        }

        private async ValueTask<(PacketHeaders, int, bool)> TransmissionControl(Memory<byte> output, CancellationToken token)
        {
            MessageType responseType;
            int count;
            bool isContinueReceiving;
            var resultTask = Cast<Task<Result<bool>>>(task);
            var stateTask = transmissionStateTrigger.WaitAsync(this, IsValidStateForResponsePredicate, token);

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
                // should be in sync with IsValidStateForResponse
                switch (state)
                {
                    case State.ReceivingEntriesFinished:
                        count = IExchange.WriteResult(await resultTask.ConfigureAwait(false), output.Span);
                        isContinueReceiving = false;
                        responseType = MessageType.None;
                        break;
                    case State.ReadyToReceiveEntry:
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
            return new ValueTask();
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    using IO;
    using IO.Log;
    using Threading;
    using static IO.Pipelines.ResultExtensions;
    using static Runtime.Intrinsics;

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct ReceivedLogEntry : IRaftLogEntry
    {
        private readonly PipeReader reader;
        private readonly long term, length;
        private readonly DateTimeOffset timeStamp;
        private readonly bool isSnapshot;

        public ReceivedLogEntry(ref ReadOnlySpan<byte> prologue, PipeReader reader)
        {
            var count = EntriesExchange.ParseLogEntryPrologue(prologue, out length, out term, out timeStamp, out isSnapshot);
            prologue = prologue.Slice(count);
            this.reader = reader;
        }

        long? IDataTransferObject.Length => length < 0 ? new long?() : length;

        long IRaftLogEntry.Term => term;

        DateTimeOffset ILogEntry.Timestamp => timeStamp;

        bool ILogEntry.IsSnapshot => isSnapshot;

        bool IDataTransferObject.IsReusable => false;

        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => new ValueTask(writer.CopyFromAsync(reader, token));
        ValueTask<TResult> IDataTransferObject.GetObjectDataAsync<TResult, TDecoder>(TDecoder parser, CancellationToken token)
            => IDataTransferObject.DecodeAsync<TResult, TDecoder>(reader, parser, token);
    }

    internal partial class ServerExchange : ILogEntryProducer<ReceivedLogEntry>
    {
        private readonly Action<ServerExchange, State> setStateAction;
        private readonly Predicate<ServerExchange> isReadyToReadEntryPredicate;
        private readonly Predicate<ServerExchange> isValidStateForResponsePredicate;

        private int remainingCount, lookupIndex;
        private ReceivedLogEntry currentEntry;
        private readonly AsyncTrigger transmissionStateTrigger = new AsyncTrigger();

        long ILogEntryProducer<ReceivedLogEntry>.RemainingCount => remainingCount;

        ReceivedLogEntry IAsyncEnumerator<ReceivedLogEntry>.Current => currentEntry;

        private void SetState(State newState) => state = newState;
        
        private bool IsReadyToReadEntry() => state == State.ReadyToReadEntry;

        private bool IsValidStateForResponse()
            => state.IsOneOf(State.ReceivingEntriesFinished, State.ReadyToReceiveEntry, State.ReadyToReadEntry);

        ValueTask<bool> IAsyncEnumerator<ReceivedLogEntry>.MoveNextAsync()
        {
            //at the moment of this method call entire exchange can be in the following states:
            //log entry headers are obtained and entire log entry ready to read
            //log entry content is completely obtained
            //iteration was not started
            //no more entries to enumerate
            ValueTask<bool> result;
            if(remainingCount <= 0) //no more entries to enumerate
            {
                //resume wait thread to finalize response
                transmissionStateTrigger.Signal(this, setStateAction, State.ReceivingEntriesFinished);
                result = new ValueTask<bool>(false);
            }
            else
            {
                lookupIndex += 1;
                remainingCount -= 1;
                //inform that we ready to receive entry and wait when it become available
                result = new ValueTask<bool>(transmissionStateTrigger.SignalAndWaitAsync(this, setStateAction, State.ReadyToReceiveEntry, isReadyToReadEntryPredicate, InfiniteTimeSpan));
            }
            return result;
        }

        private void BeginReceiveEntries(EndPoint sender, ReadOnlySpan<byte> announcement, CancellationToken token)
        {
            lookupIndex = -1;
            state = State.AppendEntriesReceived;
            EntriesExchange.ParseAnnouncement(announcement, out var term, out var prevLogIndex, out var prevLogTerm, out var commitIndex, out remainingCount);
            task = server.ReceiveEntriesAsync(sender, term, this, prevLogIndex, prevLogTerm, commitIndex, token);
        }

        private void BeginReceiveEntry(ReadOnlySpan<byte> prologue)
        {
            currentEntry = new ReceivedLogEntry(ref prologue, Reader);
            if(!prologue.IsEmpty)
            {
                var memory = Writer.GetSpan(prologue.Length);
                prologue.CopyTo(memory);
                Writer.Advance(prologue.Length);
            }
            transmissionStateTrigger.Signal(this, setStateAction, State.ReadyToReadEntry);
        }

        private async ValueTask<bool> ReceivingEntry(ReadOnlyMemory<byte> content, bool completed, CancellationToken token)
        {
            if(content.IsEmpty)
                completed = true;
            else
            {
                var result = await Writer.WriteAsync(content, token).ConfigureAwait(false);
                result.ThrowIfCancellationRequested(token);
                completed |= result.IsCompleted;
            }
            if(completed)
            {
                transmissionStateTrigger.Signal(this, setStateAction, State.AppendEntriesReceived);
                await Writer.CompleteAsync().ConfigureAwait(false);
            }
            return true;
        }

        private async ValueTask<(PacketHeaders, int, bool)> TransmissionControl(Memory<byte> output, CancellationToken token)
        {
            int count;
            bool isContinueReceiving;
            var resultTask = Cast<Task<Result<bool>>>(task);
            var stateTask = transmissionStateTrigger.WaitAsync(this, isValidStateForResponsePredicate, token);
            //wait for result or state transition
            if(ReferenceEquals(resultTask, await Task.WhenAny(resultTask, stateTask).ConfigureAwait(false)))
            {
                //result obtained, finalize transmission
                task = null;
                state = State.ReceivingEntriesFinished;
                remainingCount = 0;
                count = EntriesExchange.CreateResponse(resultTask.Result, output.Span);
                isContinueReceiving = false;
            }
            else
                switch(state)   //should be in sync with IsValidStateForResponse
                {
                    case State.ReceivingEntriesFinished:
                        count = EntriesExchange.CreateResponse(await resultTask.ConfigureAwait(false), output.Span);
                        isContinueReceiving = false;
                        break;
                    case State.ReadyToReceiveEntry:
                        ReusePipe();
                        count = EntriesExchange.CreateNextEntryResponse(output.Span, lookupIndex);
                        isContinueReceiving = true;
                        break;
                    case State.ReadyToReadEntry:
                        count = EntriesExchange.CreateContinueResponse(output.Span);
                        isContinueReceiving = true;
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            return (new PacketHeaders(MessageType.AppendEntries, FlowControl.Ack), count, isContinueReceiving);
        }

        ValueTask IAsyncDisposable.DisposeAsync()
        {
            remainingCount = -1;
            return new ValueTask();
        }   
    }
}
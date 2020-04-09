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

        internal ReceivedLogEntry(ref ReadOnlyMemory<byte> prologue, PipeReader reader)
        {
            var count = EntriesExchange.ParseLogEntryPrologue(prologue.Span, out length, out term, out timeStamp, out isSnapshot);
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
        private readonly Predicate<ServerExchange> isReadyToReadEntry, isValidStateForResponse, isValidForTransition;

        private int remainingCount, lookupIndex;
        private ReceivedLogEntry currentEntry;
        private readonly AsyncTrigger transmissionStateTrigger = new AsyncTrigger();

        long ILogEntryProducer<ReceivedLogEntry>.RemainingCount => remainingCount;

        ReceivedLogEntry IAsyncEnumerator<ReceivedLogEntry>.Current => currentEntry;

        private void SetState(State newState) => state = newState;
        
        private bool IsReadyToReadEntry() => state == State.ReadyToReadEntry;

        private bool IsValidStateForResponse()
            => state.IsOneOf(State.ReceivingEntriesFinished, State.ReadyToReceiveEntry, State.ReadyToReadEntry);

        private bool IsValidForTransition() => state == State.AppendEntriesReceived;

        async ValueTask<bool> IAsyncEnumerator<ReceivedLogEntry>.MoveNextAsync()
        {
            //at the moment of this method call entire exchange can be in the following states:
            //log entry headers are obtained and entire log entry ready to read
            //log entry content is completely obtained
            //iteration was not started
            //no more entries to enumerate
            if(remainingCount <= 0) //no more entries to enumerate
            {
                //resume wait thread to finalize response
                transmissionStateTrigger.Signal(this, setStateAction, State.ReceivingEntriesFinished);
                return false;
            }
            if(lookupIndex >= 0)
            {
                await Reader.CompleteAsync().ConfigureAwait(false);
                await transmissionStateTrigger.WaitAsync(this, isValidForTransition).ConfigureAwait(false);
                ReusePipe(false);
            }
            lookupIndex += 1;
            remainingCount -= 1;
            
            return await transmissionStateTrigger.SignalAndWaitAsync(this, setStateAction, State.ReadyToReceiveEntry, isReadyToReadEntry, InfiniteTimeSpan).ConfigureAwait(false);
        }

        private void BeginReceiveEntries(EndPoint sender, ReadOnlySpan<byte> announcement, CancellationToken token)
        {
            lookupIndex = -1;
            state = State.AppendEntriesReceived;
            EntriesExchange.ParseAnnouncement(announcement, out var term, out var prevLogIndex, out var prevLogTerm, out var commitIndex, out remainingCount);
            task = server.ReceiveEntriesAsync(sender, term, this, prevLogIndex, prevLogTerm, commitIndex, token);
        }

        private void BeginReceiveEntry(ReadOnlyMemory<byte> prologue)
        {
            currentEntry = new ReceivedLogEntry(ref prologue, Reader);
            var memory = Writer.GetMemory(prologue.Length);
            prologue.CopyTo(memory);
            Writer.Advance(prologue.Length);
            transmissionStateTrigger.Signal(this, setStateAction, State.ReadyToReadEntry);
        }

        private async ValueTask<bool> ReceivingEntry(ReadOnlyMemory<byte> content, bool completed, CancellationToken token)
        {
            if(content.IsEmpty)
                completed = true;
            else
            {
                var result = await Writer.WriteAsync(content, token).ConfigureAwait(false);
                completed |= result.IsCompleted;
            }
            if(completed)
            {
                await Writer.CompleteAsync().ConfigureAwait(false);
                transmissionStateTrigger.Signal(this, setStateAction, State.AppendEntriesReceived);
            }
            return true;
        }

        private async ValueTask<(PacketHeaders, int, bool)> TransmissionControl(Memory<byte> output, CancellationToken token)
        {
            int count;
            bool isContinueReceiving;
            var resultTask = Cast<Task<Result<bool>>>(task);
            var stateTask = transmissionStateTrigger.WaitAsync(this, isValidStateForResponse, token);
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
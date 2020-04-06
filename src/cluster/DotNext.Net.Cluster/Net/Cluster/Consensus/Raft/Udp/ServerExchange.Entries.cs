using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    using IO;
    using IO.Log;
    using Threading;

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct ReceivedLogEntry : IRaftLogEntry
    {
        private readonly PipeReader reader;

        public ReceivedLogEntry(long term, DateTimeOffset timestamp, bool isSnapshot, long length, PipeReader reader)
        {
            Term = term;
            Timestamp = timestamp;
            IsSnapshot = isSnapshot;
            Length = length < 0 ? new long?() : length;
            this.reader = reader;
        }

        public long? Length { get; }

        public long Term { get; }

        public DateTimeOffset Timestamp { get; }

        public bool IsSnapshot { get; }

        bool IDataTransferObject.IsReusable => false;

        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => new ValueTask(writer.CopyFromAsync(reader, token));
    }

    internal partial class ServerExchange : ILogEntryProducer<ReceivedLogEntry>
    {
        private int remainingCount, lookupIndex;
        private ReceivedLogEntry currentEntry;
        private readonly AsyncManualResetEvent responseTrigger = new AsyncManualResetEvent(false);

        long ILogEntryProducer<ReceivedLogEntry>.RemainingCount => remainingCount;

        ReceivedLogEntry IAsyncEnumerator<ReceivedLogEntry>.Current => currentEntry;

        ValueTask<bool> IAsyncEnumerator<ReceivedLogEntry>.MoveNextAsync()
        {
            bool result;
            if(state == State.ReceivingEntriesFinished)
            {
                result = false;
                goto exit;
            }
            else if(remainingCount <= 0)
            {
                state = State.ReceivingEntriesFinished;
                result = false;
            }
            else
            {
                remainingCount -= 1;
                lookupIndex += 1;
                state = State.ReadyToReceiveEntry;
                result = true;
            }
            responseTrigger.Set(true);
            exit:
            return new ValueTask<bool>(result);
        }

        private void BeginReceiveEntries(EndPoint sender, ReadOnlySpan<byte> announcement, CancellationToken token)
        {
            lookupIndex = -1;
            EntriesExchange.ParseAnnouncement(announcement, out var term, out var prevLogIndex, out var prevLogTerm, out var commitIndex, out remainingCount);
            task = server.ReceiveEntriesAsync(sender, term, this, prevLogIndex, prevLogTerm, commitIndex, token);
        }

        ValueTask IAsyncDisposable.DisposeAsync()
        {
            remainingCount = lookupIndex = -1;
            responseTrigger.Set();
            return new ValueTask();
        }   
    }
}
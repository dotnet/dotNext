using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    using Buffers;
    using static IO.DataTransferObject;
    using static IO.Pipelines.PipeExtensions;

    internal abstract class EntriesExchange : ClientExchange<Result<bool>>, IAsyncDisposable
    {
        /*
            Message flow:
            1.REQ(None) Announce number of entries, prevLogIndex, prevLogTerm etc.
            1.RES(Ack) Wait for command: NextEntry to start sending content, None to abort transmission

            2.REQ(StreamStart) with information about content-type and length of the record
            2.REP(Ack) Wait for command: NextEntry to start sending content, Continue to send next chunk, None to finalize transmission

            3.REQ(Fragment) with the chunk of record data
            3.REP(Ack) Wait for command: NextEntry to start sending content, Continue to send next chunk, None to finalize transmission

            4.REQ(StreamEnd) with the final chunk of record data
            4.REP(Ack) Wait for command: NextEntry to start sending content, None to finalize transmission
        */
        private protected readonly Pipe pipe;
        private readonly long term, prevLogIndex, prevLogTerm, commitIndex;
        private readonly EmptyClusterConfiguration? configuration;

        internal EntriesExchange(long term, long prevLogIndex, long prevLogTerm, long commitIndex, EmptyClusterConfiguration? configState, PipeOptions? options = null)
        {
            pipe = new Pipe(options ?? PipeOptions.Default);
            this.term = term;
            this.prevLogIndex = prevLogIndex;
            this.prevLogTerm = prevLogTerm;
            this.commitIndex = commitIndex;
            configuration = configState;
        }

        internal static int CreateNextEntryResponse(Span<byte> output, int logEntryIndex)
        {
            WriteInt32LittleEndian(output, logEntryIndex);
            return sizeof(int);
        }

        internal static int ParseLogEntryPrologue(ReadOnlySpan<byte> input, out LogEntryMetadata metadata)
        {
            var reader = new SpanReader<byte>(input);
            metadata = new LogEntryMetadata(ref reader);
            return LogEntryMetadata.Size;
        }

        internal static void ParseAnnouncement(ReadOnlySpan<byte> input, out ClusterMemberId sender, out long term, out long prevLogIndex, out long prevLogTerm, out long commitIndex, out int entriesCount, out EmptyClusterConfiguration? configuration)
        {
            var reader = new SpanReader<byte>(input);

            sender = new(ref reader);
            term = reader.ReadInt64(true);
            prevLogIndex = reader.ReadInt64(true);
            prevLogTerm = reader.ReadInt64(true);
            commitIndex = reader.ReadInt64(true);
            entriesCount = reader.ReadInt32(true);
            configuration = EmptyClusterConfiguration.ReadFrom(ref reader);
        }

        private protected int WriteAnnouncement(Span<byte> output, int entriesCount)
        {
            var writer = new SpanWriter<byte>(output);

            sender.WriteTo(ref writer);
            writer.WriteInt64(term, true);
            writer.WriteInt64(prevLogIndex, true);
            writer.WriteInt64(prevLogTerm, true);
            writer.WriteInt64(commitIndex, true);
            writer.WriteInt32(entriesCount, true);
            EmptyClusterConfiguration.WriteTo(in configuration, ref writer);

            return writer.WrittenCount;
        }

        private protected sealed override void OnException(Exception e) => pipe.Writer.Complete(e);

        private protected sealed override void OnCanceled(CancellationToken token) => OnException(new OperationCanceledException(token));

        internal void AbortIO()
        {
            pipe.Writer.CancelPendingFlush();
            pipe.Reader.CancelPendingRead();
        }

        async ValueTask IAsyncDisposable.DisposeAsync()
        {
            var e = new ObjectDisposedException(GetType().Name);
            await pipe.Writer.CompleteAsync(e).ConfigureAwait(false);
            await pipe.Reader.CompleteAsync(e).ConfigureAwait(false);
        }
    }

    internal abstract class EntriesExchange<TEntry> : EntriesExchange
        where TEntry : IRaftLogEntry
    {
        private delegate ValueTask<FlushResult> LogEntryFragmentWriter(PipeWriter writer, TEntry entry, CancellationToken token);

        private static readonly LogEntryFragmentWriter[] FragmentWriters =
        {
            WriteLogEntryMetadata,
            WriteLogEntryContent,
        };

        private protected EntriesExchange(long term, long prevLogIndex, long prevLogTerm, long commitIndex, EmptyClusterConfiguration? configState, PipeOptions? options = null)
            : base(term, prevLogIndex, prevLogTerm, commitIndex, configState, options)
        {
        }

        private static ValueTask<FlushResult> WriteLogEntryMetadata(PipeWriter writer, TEntry entry, CancellationToken token)
        {
            var buffer = writer.GetSpan(LogEntryMetadata.Size);
            LogEntryMetadata.Create(entry).Serialize(buffer.Slice(0, LogEntryMetadata.Size));
            writer.Advance(LogEntryMetadata.Size);
            return writer.FlushAsync(token);
        }

        private static async ValueTask<FlushResult> WriteLogEntryContent(PipeWriter writer, TEntry entry, CancellationToken token)
        {
            var canceled = false;
            try
            {
                await entry.WriteToAsync(writer, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                canceled = true;
            }

            return new FlushResult(canceled, false);
        }

        internal static async Task WriteEntryAsync(PipeWriter writer, TEntry entry, CancellationToken token)
        {
            foreach (var serializer in FragmentWriters)
            {
                var flushResult = await serializer(writer, entry, token).ConfigureAwait(false);
                if (flushResult.IsCompleted)
                    return;
                if (flushResult.IsCanceled)
                    break;
            }

            await writer.CompleteAsync().ConfigureAwait(false);
        }
    }

    internal sealed class EntriesExchange<TEntry, TList> : EntriesExchange<TEntry>
        where TEntry : IRaftLogEntry
        where TList : IReadOnlyList<TEntry>
    {
        private TList entries;

        private Task? writeSession;
        private int currentIndex;
        private bool streamStart;

        internal EntriesExchange(long term, in TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, EmptyClusterConfiguration? configState, PipeOptions? options = null)
            : base(term, prevLogIndex, prevLogTerm, commitIndex, configState, options)
        {
            this.entries = entries;
            currentIndex = -1;
        }

        public override async ValueTask<(PacketHeaders, int, bool)> CreateOutboundMessageAsync(Memory<byte> payload, CancellationToken token)
        {
            int count;
            FlowControl control;

            // write portion of log entry
            if (currentIndex >= 0)
            {
                count = await pipe.Reader.CopyToAsync(payload, token).ConfigureAwait(false);
                if (count == payload.Length)
                    control = streamStart ? FlowControl.StreamStart : FlowControl.Fragment;
                else
                    control = FlowControl.StreamEnd;
            }
            else
            {
                // send announcement
                count = WriteAnnouncement(payload.Span, entries.Count);
                control = FlowControl.None;
            }

            return (new PacketHeaders(MessageType.AppendEntries, control), count, true);
        }

        private void FinalizeTransmission(ReadOnlySpan<byte> input)
        {
            TrySetResult(IExchange.ReadResult(input));
            writeSession = null;
        }

        private Task WriteEntryAsync(CancellationToken token)
            => WriteEntryAsync(pipe.Writer, entries[currentIndex], token);

        private async Task NextEntryAsync(ReadOnlyMemory<byte> input, CancellationToken token)
        {
            currentIndex = ReadInt32LittleEndian(input.Span);
            if (writeSession is not null)
            {
                AbortIO();
                await writeSession.ConfigureAwait(false);
                await pipe.Reader.CompleteAsync().ConfigureAwait(false);
                pipe.Reset();
            }

            writeSession = WriteEntryAsync(token);
        }

        public override async ValueTask<bool> ProcessInboundMessageAsync(PacketHeaders headers, ReadOnlyMemory<byte> payload, CancellationToken token)
        {
            switch (headers.Type)
            {
                default:
                    return false;
                case MessageType.None:
                    FinalizeTransmission(payload.Span);
                    return false;
                case MessageType.NextEntry:
                    streamStart = true;
                    await NextEntryAsync(payload, token).ConfigureAwait(false);
                    return true;
                case MessageType.Continue:
                    streamStart = false;
                    return true;
            }
        }
    }
}
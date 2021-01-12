using System;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using static System.Buffers.Binary.BinaryPrimitives;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    using Buffers;
    using static IO.DataTransferObject;
    using static IO.Pipelines.PipeExtensions;

    internal sealed class SnapshotExchange : ClientExchange<Result<bool>>, IAsyncDisposable
    {
        private readonly Pipe pipe;
        private readonly long term, snapshotIndex;
        private readonly IRaftLogEntry snapshot;
        private Task? transmission;

        internal SnapshotExchange(long term, IRaftLogEntry snapshot, long snapshotIndex, PipeOptions? options = null)
        {
            this.term = term;
            this.snapshotIndex = snapshotIndex;
            this.snapshot = snapshot;
            pipe = new Pipe(options ?? PipeOptions.Default);
        }

        internal static int ParseAnnouncement(ReadOnlySpan<byte> input, out ushort remotePort, out long term, out long snapshotIndex, out long length, out long snapshotTerm, out DateTimeOffset timestamp)
        {
            var reader = new SpanReader<byte>(input);

            remotePort = ReadUInt16LittleEndian(reader.Read(sizeof(ushort)));
            term = ReadInt64LittleEndian(reader.Read(sizeof(long)));
            snapshotIndex = ReadInt64LittleEndian(reader.Read(sizeof(long)));
            length = ReadInt64LittleEndian(reader.Read(sizeof(long)));
            snapshotTerm = ReadInt64LittleEndian(reader.Read(sizeof(long)));
            timestamp = reader.Read<DateTimeOffset>();

            return reader.ConsumedCount;
        }

        private int WriteAnnouncement(Span<byte> output)
        {
            var writer = new SpanWriter<byte>(output);

            WriteUInt16LittleEndian(writer.Slide(sizeof(ushort)), myPort);
            WriteInt64LittleEndian(writer.Slide(sizeof(long)), term);
            WriteInt64LittleEndian(writer.Slide(sizeof(long)), snapshotIndex);
            WriteInt64LittleEndian(writer.Slide(sizeof(long)), snapshot.Length.GetValueOrDefault(-1));
            WriteInt64LittleEndian(writer.Slide(sizeof(long)), snapshot.Term);
            writer.Write(snapshot.Timestamp);

            return writer.WrittenCount;
        }

        private async Task WriteSnapshotAsync(CancellationToken token)
        {
            await snapshot.WriteToAsync(pipe.Writer, token).ConfigureAwait(false);
            await pipe.Writer.CompleteAsync().ConfigureAwait(false);
        }

        public override async ValueTask<(PacketHeaders, int, bool)> CreateOutboundMessageAsync(Memory<byte> payload, CancellationToken token)
        {
            var count = default(int);
            FlowControl control;
            if (transmission is null)
            {
                count = WriteAnnouncement(payload.Span);
                payload = payload.Slice(count);
                control = FlowControl.StreamStart;
                transmission = WriteSnapshotAsync(token);
            }
            else
            {
                control = FlowControl.Fragment;
            }

            count += await pipe.Reader.CopyToAsync(payload, token).ConfigureAwait(false);
            if (count < payload.Length)
                control = FlowControl.StreamEnd;
            return (new PacketHeaders(MessageType.InstallSnapshot, control), count, true);
        }

        public override ValueTask<bool> ProcessInboundMessageAsync(PacketHeaders headers, ReadOnlyMemory<byte> payload, EndPoint endpoint, CancellationToken token)
        {
            if (headers.Type == MessageType.Continue)
                return new ValueTask<bool>(true);

            TrySetResult(IExchange.ReadResult(payload.Span));
            return default;
        }

        async ValueTask IAsyncDisposable.DisposeAsync()
        {
            var e = new ObjectDisposedException(GetType().Name);
            await pipe.Writer.CompleteAsync(e).ConfigureAwait(false);
            await pipe.Reader.CompleteAsync(e).ConfigureAwait(false);
        }
    }
}
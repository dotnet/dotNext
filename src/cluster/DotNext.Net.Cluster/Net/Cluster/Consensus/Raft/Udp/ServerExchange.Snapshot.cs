using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    using static Runtime.Intrinsics;

    internal partial class ServerExchange
    {
        private void BeginReceiveSnapshot(ReadOnlySpan<byte> input, EndPoint endPoint, CancellationToken token)
        {
            var snapshot = new ReceivedLogEntry(input, Reader, out var senderTerm, out var snapshotIndex);
            task = server.ReceiveSnapshotAsync(endPoint, senderTerm, snapshot, snapshotIndex, token);
        }

        private async ValueTask<bool> ReceivingSnapshot(ReadOnlyMemory<byte> content, bool completed, CancellationToken token)
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
                state = State.ReceivingSnapshotFinished;
            }
            return true;
        }

        private ValueTask<(PacketHeaders, int, bool)> RequestSnapshotChunk(Span<byte> output)
        {
            output[0] = (byte)1;
            return new ValueTask<(PacketHeaders, int, bool)>((new PacketHeaders(MessageType.InstallSnapshot, FlowControl.Ack), 1, true));
        }

        private async ValueTask<(PacketHeaders, int, bool)> EndReceiveSnapshot(Memory<byte> output)
        {
            output.Span[0] = (byte)0;
            output = output.Slice(sizeof(byte));
            var result = await Cast<Task<Result<bool>>>(Interlocked.Exchange(ref task, null)).ConfigureAwait(false);
            return (new PacketHeaders(MessageType.InstallSnapshot, FlowControl.Ack), IExchange.WriteResult(result, output.Span) + sizeof(byte), false);
        }
    }
}
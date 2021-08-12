using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    using static Runtime.Intrinsics;

    internal partial class ServerExchange
    {
        private async ValueTask<bool> BeginReceiveSnapshot(ReadOnlyMemory<byte> input, EndPoint endPoint, bool completed, CancellationToken token)
        {
            var snapshot = new ReceivedLogEntry(ref input, Reader, out var remotePort, out var senderTerm, out var snapshotIndex);
            var result = await Writer.WriteAsync(input, token).ConfigureAwait(false);
            ChangePort(ref endPoint, remotePort);
            task = server.InstallSnapshotAsync(endPoint, senderTerm, snapshot, snapshotIndex, token);
            if (result.IsCompleted | completed)
            {
                await Writer.CompleteAsync().ConfigureAwait(false);
                state = State.ReceivingSnapshotFinished;
            }

            return true;
        }

        private async ValueTask<bool> ReceivingSnapshot(ReadOnlyMemory<byte> content, bool completed, CancellationToken token)
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
                state = State.ReceivingSnapshotFinished;
            }

            return true;
        }

        private static ValueTask<(PacketHeaders, int, bool)> RequestSnapshotChunk()
            => new((new PacketHeaders(MessageType.Continue, FlowControl.Ack), 0, true));

        private async ValueTask<(PacketHeaders, int, bool)> EndReceiveSnapshot(Memory<byte> output)
        {
            var result = await Cast<Task<Result<bool>>>(Interlocked.Exchange(ref task, null)).ConfigureAwait(false);
            return (new PacketHeaders(MessageType.None, FlowControl.Ack), IExchange.WriteResult(result, output.Span) + sizeof(byte), false);
        }
    }
}
using BinaryPrimitives = System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.Datagram;

using static Runtime.Intrinsics;

internal partial class ServerExchange
{
    private void BeginSynchronize(ReadOnlyMemory<byte> payload, CancellationToken token)
        => task = server.SynchronizeAsync(BinaryPrimitives.ReadInt64LittleEndian(payload.Span), token).AsTask();

    private async ValueTask<(PacketHeaders, int, bool)> EndSynchronize(Memory<byte> payload)
    {
        var result = await Cast<Task<long?>>(Interlocked.Exchange(ref task, null)).ConfigureAwait(false);
        return (new PacketHeaders(MessageType.Synchronize, FlowControl.Ack), SynchronizeExchange.WriteResponse(payload.Span, result), false);
    }
}
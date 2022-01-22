using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.Datagram;

using Buffers;

internal sealed class SynchronizeExchange : ClientExchange<long?>
{
    public override ValueTask<bool> ProcessInboundMessageAsync(PacketHeaders headers, ReadOnlyMemory<byte> payload, CancellationToken token)
    {
        Debug.Assert(headers.Control == FlowControl.Ack);

        var reader = new SpanReader<byte>(payload.Span);
        var hasValue = ValueTypeExtensions.ToBoolean(reader.Read());
        var commitIndex = reader.ReadInt64(true);
        TrySetResult(hasValue ? commitIndex : null);
        return new(false);
    }

    internal static int WriteResponse(Span<byte> output, long? commitIndex)
    {
        var writer = new SpanWriter<byte>(output);
        writer.Add(commitIndex.HasValue.ToByte());
        writer.WriteInt64(commitIndex.GetValueOrDefault(), true);
        return writer.WrittenCount;
    }

    public override ValueTask<(PacketHeaders Headers, int BytesWritten, bool)> CreateOutboundMessageAsync(Memory<byte> payload, CancellationToken token)
        => new((new PacketHeaders(MessageType.Synchronize, FlowControl.None), 0, true));
}
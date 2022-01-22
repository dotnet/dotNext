using System.IO.Pipelines;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.Datagram;

using Serializable = Runtime.Serialization.Serializable;

internal sealed class MetadataExchange : PipeExchange, IClientExchange<Task<IReadOnlyDictionary<string, string>>>
{
    private bool state;

    internal MetadataExchange(PipeOptions? options = null)
        : base(options)
    {
    }

    // id announcement is not used for this request
    ClusterMemberId IClientExchange<Task<IReadOnlyDictionary<string, string>>>.Sender
    {
        set { }
    }

    async Task<IReadOnlyDictionary<string, string>> ISupplier<CancellationToken, Task<IReadOnlyDictionary<string, string>>>.Invoke(CancellationToken token)
#pragma warning disable CA2252  // TODO: Remove in .NET 7
        => (await Serializable.ReadFromAsync<MetadataTransferObject>(Reader, token).ConfigureAwait(false)).Metadata;
#pragma warning restore CA2252

    public override async ValueTask<bool> ProcessInboundMessageAsync(PacketHeaders headers, ReadOnlyMemory<byte> payload, CancellationToken token)
    {
        var flushResult = await Writer.WriteAsync(payload, token).ConfigureAwait(false);
        return flushResult is { IsCanceled: false, IsCompleted: false } && headers.Control is not FlowControl.StreamEnd;
    }

    public override ValueTask<(PacketHeaders, int, bool)> CreateOutboundMessageAsync(Memory<byte> payload, CancellationToken token)
    {
        FlowControl control;
        if (state)
        {
            control = FlowControl.Ack;
        }
        else
        {
            state = true;
            control = FlowControl.None;
        }

        return new((new PacketHeaders(MessageType.Metadata, control), 0, true));
    }
}
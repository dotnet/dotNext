using Microsoft.AspNetCore.Connections;

namespace DotNext.Net.Cluster.Consensus.Raft.Membership;

using Buffers;

internal sealed class InMemoryClusterConfigurationStorage : InMemoryClusterConfigurationStorage<UriEndPoint>
{
    protected override IEqualityComparer<UriEndPoint> Comparer => EndPointFormatter.UriEndPointComparer;

    protected override void Encode(UriEndPoint address, ref BufferWriterSlim<byte> writer)
        => writer.WriteEndPoint(address);

    protected override UriEndPoint Decode(ref SequenceReader reader)
        => (UriEndPoint)reader.ReadEndPoint();
}
using Microsoft.AspNetCore.Connections;

namespace DotNext.Net.Cluster.Consensus.Raft.Membership;

using Buffers;

internal sealed class PersistentClusterConfigurationStorage(string fileName) : PersistentClusterConfigurationStorage<UriEndPoint>(fileName)
{
    protected override IEqualityComparer<UriEndPoint> Comparer => EndPointFormatter.UriEndPointComparer;

    protected override void Encode(UriEndPoint address, ref BufferWriterSlim<byte> writer)
        => writer.WriteEndPoint(address);

    protected override UriEndPoint Decode(ref SequenceReader reader)
        => (UriEndPoint)reader.ReadEndPoint();
}
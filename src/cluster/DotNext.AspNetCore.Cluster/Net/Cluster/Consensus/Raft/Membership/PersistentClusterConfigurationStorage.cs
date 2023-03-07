using Microsoft.AspNetCore.Connections;

namespace DotNext.Net.Cluster.Consensus.Raft.Membership;

using Buffers;
using IO;

internal sealed class PersistentClusterConfigurationStorage : PersistentClusterConfigurationStorage<UriEndPoint>
{
    internal PersistentClusterConfigurationStorage(string path)
        : base(path, comparer: EndPointFormatter.UriEndPointComparer)
    {
    }

    protected override void Encode(UriEndPoint address, ref BufferWriterSlim<byte> output)
        => output.WriteEndPoint(address);

    protected override UriEndPoint Decode(ref SequenceReader reader)
        => (UriEndPoint)reader.ReadEndPoint();
}
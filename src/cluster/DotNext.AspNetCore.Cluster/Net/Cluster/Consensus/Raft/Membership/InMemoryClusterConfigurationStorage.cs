namespace DotNext.Net.Cluster.Consensus.Raft.Membership
{
    using Buffers;
    using IO;

    internal sealed class InMemoryClusterConfigurationStorage : InMemoryClusterConfigurationStorage<HttpEndPoint>
    {
        internal InMemoryClusterConfigurationStorage()
        {
        }

        protected override void Encode(HttpEndPoint address, ref BufferWriterSlim<byte> output)
            => output.WriteEndPoint(address);

        protected override HttpEndPoint Decode(ref SequenceBinaryReader reader)
            => (HttpEndPoint)reader.ReadEndPoint();
    }
}
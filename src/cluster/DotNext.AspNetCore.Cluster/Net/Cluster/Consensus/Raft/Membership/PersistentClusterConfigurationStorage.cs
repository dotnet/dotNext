namespace DotNext.Net.Cluster.Consensus.Raft.Membership
{
    using Buffers;
    using IO;

    internal sealed class PersistentClusterConfigurationStorage : PersistentClusterConfigurationStorage<HttpEndPoint>
    {
        internal PersistentClusterConfigurationStorage(string path)
            : base(path)
        {
        }

        protected override void Encode(HttpEndPoint address, ref BufferWriterSlim<byte> output)
            => output.WriteEndPoint(address);

        protected override HttpEndPoint Decode(ref SequenceReader reader)
            => (HttpEndPoint)reader.ReadEndPoint();
    }
}
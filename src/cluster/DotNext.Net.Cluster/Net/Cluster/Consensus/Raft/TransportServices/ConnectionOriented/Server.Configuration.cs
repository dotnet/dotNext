namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Buffers;
using IO;
using IClusterConfiguration = Membership.IClusterConfiguration;

internal partial class Server
{
    private sealed class InMemoryClusterConfiguration(MemoryOwner<byte> buffer, long fingerprint) : Disposable, IClusterConfiguration
    {
        internal InMemoryClusterConfiguration(long fingerprint)
            : this(default, fingerprint)
        {
        }

        internal Memory<byte> Content => buffer.Memory;

        public long Fingerprint => fingerprint;

        long IClusterConfiguration.Length => buffer.Length;

        bool IDataTransferObject.IsReusable => true;

        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => writer.Invoke(buffer.Memory, token);

        bool IDataTransferObject.TryGetMemory(out ReadOnlyMemory<byte> memory)
        {
            memory = Content;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                buffer.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
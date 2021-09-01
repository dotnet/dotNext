using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Membership
{
    using IO;

    internal sealed class EmptyClusterConfiguration : IClusterConfiguration
    {
        internal EmptyClusterConfiguration(long fingerprint)
            => Fingerprint = fingerprint;

        public long Fingerprint { get; }

        long IClusterConfiguration.Length => 0L;

        bool IDataTransferObject.IsReusable => true;

        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => IDataTransferObject.Empty.WriteToAsync(writer, token);

        bool IDataTransferObject.TryGetMemory(out ReadOnlyMemory<byte> memory)
        {
            memory = ReadOnlyMemory<byte>.Empty;
            return true;
        }
    }
}
namespace DotNext.Net.Cluster.Consensus.Raft.Membership;

using IO;

internal readonly struct EmptyClusterConfiguration : IClusterConfiguration
{
    public long Fingerprint { get; internal init; }

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
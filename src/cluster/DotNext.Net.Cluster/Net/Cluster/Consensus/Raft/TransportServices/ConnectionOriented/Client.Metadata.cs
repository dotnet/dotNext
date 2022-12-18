namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

internal partial class Client : RaftClusterMember
{
    private sealed class MetadataRequest : Request<IReadOnlyDictionary<string, string>>
    {
        private protected override ValueTask RequestAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.WriteMetadataRequestAsync(token);

        private protected override ValueTask<IReadOnlyDictionary<string, string>> ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadMetadataResponseAsync(buffer, token);
    }

    private protected sealed override Task<IReadOnlyDictionary<string, string>> GetMetadataAsync(CancellationToken token)
        => RequestAsync(new MetadataRequest(), token);
}
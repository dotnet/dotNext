namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

internal partial class Client : RaftClusterMember
{
    private sealed class MetadataExchange : IClientExchange<IReadOnlyDictionary<string, string>>
    {
        internal static readonly MetadataExchange Instance = new();

        private MetadataExchange()
        {
        }

        ValueTask IClientExchange<IReadOnlyDictionary<string, string>>.RequestAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.WriteMetadataRequestAsync(token);

        ValueTask<IReadOnlyDictionary<string, string>> IClientExchange<IReadOnlyDictionary<string, string>>.ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadMetadataResponseAsync(buffer, token);
    }

    private protected sealed override Task<IReadOnlyDictionary<string, string>> GetMetadataAsync(CancellationToken token)
        => RequestAsync<MetadataExchange, IReadOnlyDictionary<string, string>>(MetadataExchange.Instance, token);
}
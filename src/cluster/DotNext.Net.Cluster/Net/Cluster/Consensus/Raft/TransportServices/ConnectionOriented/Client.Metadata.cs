namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Patterns;

internal partial class Client
{
    private sealed class MetadataExchange : IClientExchange<IReadOnlyDictionary<string, string>>, ISingleton<MetadataExchange>
    {
        private const string Name = "Metadata";

        public static MetadataExchange Instance { get; } = new();

        private MetadataExchange()
        {
        }

        ValueTask IClientExchange<IReadOnlyDictionary<string, string>>.RequestAsync(ILocalMember localMember, ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
        {
            protocol.AdvanceWriteCursor(protocol.BeginRequestMessage(MessageType.Metadata).WrittenCount);
            return protocol.WriteToTransportAsync(token);
        }

        static ValueTask<IReadOnlyDictionary<string, string>> IClientExchange<IReadOnlyDictionary<string, string>>.ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadDictionaryAsync(buffer, token);

        static string IClientExchange<IReadOnlyDictionary<string, string>>.Name => Name;
    }

    private protected sealed override Task<IReadOnlyDictionary<string, string>> GetMetadataAsync(CancellationToken token)
        => RequestAsync<IReadOnlyDictionary<string, string>, MetadataExchange>(MetadataExchange.Instance, token);
}
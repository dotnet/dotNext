using System.Runtime.Versioning;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

internal partial class Client : RaftClusterMember
{
    [RequiresPreviewFeatures]
    private sealed class MetadataExchange : IClientExchange<IReadOnlyDictionary<string, string>>
    {
        private const string Name = "Metadata";

        internal static readonly MetadataExchange Instance = new();

        private MetadataExchange()
        {
        }

        ValueTask IClientExchange<IReadOnlyDictionary<string, string>>.RequestAsync(ILocalMember localMember, ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
        {
            protocol.Advance(protocol.BeginRequestMessage(MessageType.Metadata).WrittenCount);
            return protocol.WriteToTransportAsync(token);
        }

        static ValueTask<IReadOnlyDictionary<string, string>> IClientExchange<IReadOnlyDictionary<string, string>>.ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadMetadataResponseAsync(buffer, token);

        static string IClientExchange<IReadOnlyDictionary<string, string>>.Name => Name;
    }

    [RequiresPreviewFeatures]
    private protected sealed override Task<IReadOnlyDictionary<string, string>> GetMetadataAsync(CancellationToken token)
        => RequestAsync<IReadOnlyDictionary<string, string>, MetadataExchange>(MetadataExchange.Instance, token);
}
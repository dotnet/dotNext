namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

internal partial class Client : RaftClusterMember
{
    private sealed class ResignExchange : IClientExchange<bool>
    {
        internal static readonly ResignExchange Instance = new();

        private ResignExchange()
        {
        }

        ValueTask IClientExchange<bool>.RequestAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.WriteResignRequestAsync(token);

        ValueTask<bool> IClientExchange<bool>.ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadBoolAsync(token);
    }

    private protected sealed override Task<bool> ResignAsync(CancellationToken token)
        => RequestAsync<ResignExchange, bool>(ResignExchange.Instance, token);
}
namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

internal partial class Client : RaftClusterMember
{
    private sealed class ResignRequest : IClientExchange<bool>
    {
        internal static readonly ResignRequest Instance = new();

        private ResignRequest()
        {
        }

        ValueTask IClientExchange<bool>.RequestAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.WriteResignRequestAsync(token);

        ValueTask<bool> IClientExchange<bool>.ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadBoolAsync(token);
    }

    private protected sealed override Task<bool> ResignAsync(CancellationToken token)
        => RequestAsync<ResignRequest, bool>(ResignRequest.Instance, token);
}
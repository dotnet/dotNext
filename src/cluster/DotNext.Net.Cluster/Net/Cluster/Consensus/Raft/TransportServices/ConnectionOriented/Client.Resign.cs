namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

internal partial class Client : RaftClusterMember
{
    private sealed class ResignRequest : Request<bool>
    {
        private protected override ValueTask RequestAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.WriteResignRequestAsync(token);

        private protected override ValueTask<bool> ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadBoolAsync(token);
    }

    private protected sealed override Task<bool> ResignAsync(CancellationToken token)
        => RequestAsync(new ResignRequest(), token);
}
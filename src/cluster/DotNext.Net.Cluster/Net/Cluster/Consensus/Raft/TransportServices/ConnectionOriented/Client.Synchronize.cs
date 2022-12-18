namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

internal partial class Client : RaftClusterMember
{
    private sealed class SynchronizeRequest : Request<long?>
    {
        private readonly long commitIndex;

        internal SynchronizeRequest(long commitIndex) => this.commitIndex = commitIndex;

        private protected override ValueTask RequestAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.WriteSynchronizeRequestAsync(commitIndex, token);

        private protected override ValueTask<long?> ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadNullableInt64Async(token);
    }

    private protected sealed override Task<long?> SynchronizeAsync(long commitIndex, CancellationToken token)
        => RequestAsync(new SynchronizeRequest(commitIndex), token);
}
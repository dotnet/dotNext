using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

internal partial class Client : RaftClusterMember
{
    [StructLayout(LayoutKind.Auto)]
    [RequiresPreviewFeatures]
    private readonly struct SynchronizeExchange : IClientExchange<long?>
    {
        private const string Name = "Synchronize";

        private readonly long commitIndex;

        internal SynchronizeExchange(long commitIndex) => this.commitIndex = commitIndex;

        ValueTask IClientExchange<long?>.RequestAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.WriteSynchronizeRequestAsync(commitIndex, token);

        static ValueTask<long?> IClientExchange<long?>.ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadNullableInt64Async(token);

        static string IClientExchange<long?>.Name => Name;
    }

    [RequiresPreviewFeatures]
    private protected sealed override Task<long?> SynchronizeAsync(long commitIndex, CancellationToken token)
        => RequestAsync<long?, SynchronizeExchange>(new(commitIndex), token);
}
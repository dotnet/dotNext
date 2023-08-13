using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Buffers;

internal partial class Client : RaftClusterMember
{
    [StructLayout(LayoutKind.Auto)]
    [RequiresPreviewFeatures]
    private readonly struct SynchronizeExchange : IClientExchange<long?>
    {
        private const string Name = "Synchronize";

        private readonly long commitIndex;

        internal SynchronizeExchange(long commitIndex) => this.commitIndex = commitIndex;

        ValueTask IClientExchange<long?>.RequestAsync(ILocalMember localMember, ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
        {
            var writer = protocol.BeginRequestMessage(MessageType.Synchronize);
            writer.WriteInt64(commitIndex, true);
            protocol.Advance(writer.WrittenCount);

            return protocol.WriteToTransportAsync(token);
        }

        static ValueTask<long?> IClientExchange<long?>.ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadNullableInt64Async(token);

        static string IClientExchange<long?>.Name => Name;
    }

    [RequiresPreviewFeatures]
    private protected sealed override Task<long?> SynchronizeAsync(long commitIndex, CancellationToken token)
        => RequestAsync<long?, SynchronizeExchange>(new(commitIndex), token);
}
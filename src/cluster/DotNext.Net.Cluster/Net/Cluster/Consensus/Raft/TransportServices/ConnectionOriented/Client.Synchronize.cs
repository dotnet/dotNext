using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Buffers;

internal partial class Client
{
    [StructLayout(LayoutKind.Auto)]
    private readonly struct SynchronizeExchange : IClientExchange<long?>
    {
        private const string Name = "Synchronize";

        private readonly long commitIndex;

        internal SynchronizeExchange(long commitIndex) => this.commitIndex = commitIndex;

        ValueTask IClientExchange<long?>.RequestAsync(ILocalMember localMember, ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
        {
            var writer = protocol.BeginRequestMessage(MessageType.Synchronize);
            writer.WriteLittleEndian(commitIndex);
            protocol.AdvanceWriteCursor(writer.WrittenCount);

            return protocol.WriteToTransportAsync(token);
        }

        static ValueTask<long?> IClientExchange<long?>.ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadNullableInt64Async(token);

        static string IClientExchange<long?>.Name => Name;
    }

    private protected sealed override Task<long?> SynchronizeAsync(long commitIndex, CancellationToken token)
        => RequestAsync<long?, SynchronizeExchange>(new(commitIndex), token);
}
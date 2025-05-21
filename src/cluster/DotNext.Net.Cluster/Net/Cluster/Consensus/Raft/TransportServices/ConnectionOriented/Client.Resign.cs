namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Patterns;

internal partial class Client
{
    private sealed class ResignExchange : IClientExchange<bool>, ISingleton<ResignExchange>
    {
        private const string Name = "Resign";

        public static ResignExchange Instance { get; } = new();

        private ResignExchange()
        {
        }

        ValueTask IClientExchange<bool>.RequestAsync(ILocalMember localMember, ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
        {
            protocol.AdvanceWriteCursor(protocol.BeginRequestMessage(MessageType.Resign).WrittenCount);
            return protocol.WriteToTransportAsync(token);
        }

        static ValueTask<bool> IClientExchange<bool>.ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadBoolAsync(token);

        static string IClientExchange<bool>.Name => Name;
    }

    private protected sealed override Task<bool> ResignAsync(CancellationToken token)
        => RequestAsync<bool, ResignExchange>(ResignExchange.Instance, token);
}
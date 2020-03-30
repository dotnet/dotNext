using System.Diagnostics.CodeAnalysis;
using System.Runtime.ConstrainedExecution;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    internal interface IExchangePool
    {
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        bool TryRent(PacketHeaders headers, [NotNullWhen(true)] out IExchange exchange);

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        void Release(IExchange exchange);
    }
}
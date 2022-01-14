using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.Datagram;

internal interface IExchangePool
{
    bool TryRent([MaybeNullWhen(false)] out IExchange exchange);

    void Release(IExchange exchange);
}
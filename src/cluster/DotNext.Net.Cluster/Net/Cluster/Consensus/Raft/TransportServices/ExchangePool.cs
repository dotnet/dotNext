using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

internal sealed class ExchangePool : ConcurrentBag<IReusableExchange>, IExchangePool
{
    bool IExchangePool.TryRent([MaybeNullWhen(false)] out IExchange exchange)
    {
        var result = TryTake(out var serverExchange);
        exchange = serverExchange;
        return result;
    }

    void IExchangePool.Release(IExchange exchange)
    {
        if (exchange is ServerExchange serverExchange)
        {
            serverExchange.Reset();
            Add(serverExchange);
        }
    }
}
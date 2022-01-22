using System.Collections.Concurrent;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.Datagram;

internal sealed class ChannelPool<TChannel> : ConcurrentDictionary<CorrelationId, TChannel>
        where TChannel : struct, IChannel
{
    internal ChannelPool(int backlog)
        : base(backlog, backlog)
    {
    }

    internal void ClearAndDestroyChannels(CancellationToken token = default)
    {
        foreach (var channel in Values)
        {
            using (channel)
                channel.Exchange.OnCanceled(token.IsCancellationRequested ? token : new(true));
        }

        Clear();
    }

    internal void CancellationRequested(object? correlationId, CancellationToken token)
    {
        if (correlationId is CorrelationId id && TryRemove(id, out var channel))
        {
            using (channel)
            {
                Debug.Assert(token.IsCancellationRequested);

                channel.Exchange.OnCanceled(token);
            }
        }
    }
}
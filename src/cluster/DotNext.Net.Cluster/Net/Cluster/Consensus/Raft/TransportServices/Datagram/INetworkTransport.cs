using System.Collections.Concurrent;
using System.Net;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

internal interface INetworkTransport : IDisposable
{
    /// <summary>
    /// Represents logical communication channel inside of physical connection.
    /// </summary>
    private protected interface IChannel : IDisposable
    {
        IExchange Exchange { get; }

        CancellationToken Token { get; }
    }

    private protected sealed class ChannelPool<TChannel> : ConcurrentDictionary<CorrelationId, TChannel>
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

    IPEndPoint Address { get; }
}
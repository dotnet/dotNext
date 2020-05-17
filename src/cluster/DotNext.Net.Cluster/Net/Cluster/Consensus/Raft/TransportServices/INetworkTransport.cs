using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using static System.Collections.Immutable.ImmutableHashSet;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
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

            internal void ClearAndDestroyChannels()
            {
                foreach (var channel in Values)
                {
                    using (channel)
                        channel.Exchange.OnCanceled(new CancellationToken(true));
                }

                Clear();
            }

            internal void CancellationRequested(object correlationId)
            {
                if (TryRemove((CorrelationId)correlationId, out var channel))
                {
                    try
                    {
                        Debug.Assert(channel.Token.IsCancellationRequested);
                        channel.Exchange.OnCanceled(channel.Token);
                    }
                    finally
                    {
                        channel.Dispose();
                    }
                }
            }

            internal void CancellationRequested(ref TChannel channel, CorrelationId correlationId)
            {
                if (TryRemove(correlationId, out channel))
                {
                    try
                    {
                        channel.Exchange.OnException(new OperationCanceledException(ExceptionMessages.CanceledByRemoteHost));
                    }
                    finally
                    {
                        channel.Dispose();
                    }
                }
            }

            internal void ReportError(SocketError error)
            {
                // broadcast error to all response waiters
                var e = new SocketException((int)error);
                var abortedChannels = Keys.ToImmutableHashSet();
                foreach (var id in abortedChannels)
                {
                    if (TryRemove(id, out var channel))
                    {
                        using (channel)
                            channel.Exchange.OnException(e);
                    }
                }
            }
        }

        IPEndPoint Address { get; }
    }
}
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    using Buffers;
    using TransportServices;
    using static Threading.AtomicInt64;

    internal sealed class UdpClient : UdpSocket, IClient
    {
        [StructLayout(LayoutKind.Auto)]
        private readonly struct Channel : INetworkTransport.IChannel
        {
            private readonly IExchange exchange;
            private readonly CancellationTokenRegistration cancellation;

            internal Channel(IExchange exchange, Action<object?> cancellationCallback, CorrelationId id, CancellationToken token)
            {
                this.exchange = exchange;
                cancellation = token.CanBeCanceled ? token.Register(cancellationCallback, id) : default;
            }

            CancellationToken INetworkTransport.IChannel.Token => cancellation.Token;

            IExchange INetworkTransport.IChannel.Exchange => exchange;

            internal void Complete(Exception e) => exchange.OnException(e);

            public void Dispose() => cancellation.Dispose();
        }

        private readonly Action<object?> cancellationHandler;

        // I/O management
        private readonly long applicationId;
        private readonly INetworkTransport.ChannelPool<Channel> channels;
        private readonly RefAction<Channel, CorrelationId> cancellationInvoker;
        private readonly IPEndPoint localEndPoint;
        private long streamNumber;

        internal UdpClient(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, int backlog, MemoryAllocator<byte> allocator, Func<long> appIdGenerator, ILoggerFactory loggerFactory)
            : base(remoteEndPoint, backlog, allocator, loggerFactory)
        {
            channels = new INetworkTransport.ChannelPool<Channel>(backlog);
            cancellationHandler = channels.CancellationRequested;

            applicationId = appIdGenerator();
            streamNumber = long.MinValue;
            cancellationInvoker = channels.CancellationRequested;
            this.localEndPoint = localEndPoint;
        }

        ValueTask IClient.CancelPendingRequestsAsync()
        {
            channels.ClearAndDestroyChannels();
            return new ();
        }

        private protected override void ReportError(SocketError error)
            => channels.ReportError(error);

        private protected override void EndReceive(SocketAsyncEventArgs args)
        {
            ReadOnlyMemory<byte> datagram = args.MemoryBuffer.Slice(0, args.BytesTransferred);

            // dispatch datagram to appropriate exchange
            var correlationId = new CorrelationId(datagram.Span, out var consumedBytes);
            datagram = datagram.Slice(consumedBytes);
            if (channels.TryGetValue(correlationId, out var channel))
            {
                var headers = new PacketHeaders(datagram, out consumedBytes);
                datagram = datagram.Slice(consumedBytes);
                ProcessDatagram(channels, channel, correlationId, headers, datagram, args);
            }
            else
            {
                logger.PacketDropped(correlationId, args.RemoteEndPoint);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private bool Start(IExchange exchange)
        {
            bool result;
            if (IsBound)
            {
                result = true;
            }
            else
            {
                try
                {
                    Bind(localEndPoint);
                    Start();
                    result = true;
                }
                catch (Exception e)
                {
                    exchange.OnException(e);
                    result = false;
                }
            }

            return result;
        }

        private protected override bool AllowReceiveFromAnyHost => false;

        public async void Enqueue(IExchange exchange, CancellationToken token)
        {
            if (!IsBound && !Start(exchange))
                return;
            var id = new CorrelationId(applicationId, streamNumber.IncrementAndGet());
            var channel = new Channel(exchange, cancellationHandler, id, token);
            if (channels.TryAdd(id, channel))
            {
                try
                {
                    if (token.IsCancellationRequested)
                        throw new OperationCanceledException(token);
                    if (!await SendAsync(id, channel, Address).ConfigureAwait(false))
                        throw new NotSupportedException(ExceptionMessages.UnexpectedUdpSenderBehavior);
                }
                catch (Exception e)
                {
                    if (channels.TryRemove(id, out channel))
                    {
                        using (channel)
                            channel.Complete(e);
                    }
                }
            }
            else
            {
                using (channel)
                    channel.Complete(new InvalidOperationException(ExceptionMessages.DuplicateCorrelationId));
            }
        }

        private void Cleanup(bool disposing)
        {
            if (disposing)
            {
                channels.ClearAndDestroyChannels();
            }
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                base.Dispose(disposing);
            }
            finally
            {
                Cleanup(disposing);
            }
        }
    }
}
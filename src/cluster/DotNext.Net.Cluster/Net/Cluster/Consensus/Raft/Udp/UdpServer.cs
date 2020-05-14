using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    using Buffers;
    using TransportServices;

    internal sealed class UdpServer : UdpSocket, IServer
    {
        [StructLayout(LayoutKind.Auto)]
        private readonly struct Channel : INetworkTransport.IChannel, IDisposable
        {
            private readonly IExchangePool exchangeOwner;
            private readonly IExchange exchange;
            private readonly CancellationTokenSource timeoutTokenSource;
            private readonly CancellationTokenRegistration cancellation;

            internal Channel(IExchange exchange, IExchangePool exchanges, TimeSpan timeout, Action<object> cancellationCallback, CorrelationId id)
            {
                this.exchange = exchange;
                timeoutTokenSource = new CancellationTokenSource(timeout);
                cancellation = timeoutTokenSource.Token.Register(cancellationCallback, id);
                exchangeOwner = exchanges;
            }

            IExchange INetworkTransport.IChannel.Exchange => exchange;

            public CancellationToken Token => timeoutTokenSource.Token;

            internal bool Represents(in Channel other) => ReferenceEquals(exchange, other.exchange);

            internal static void Cancel(ref Channel channel, bool throwOnFirstException)
                => channel.timeoutTokenSource.Cancel(throwOnFirstException);

            public void Dispose()
            {
                cancellation.Dispose();
                timeoutTokenSource.Dispose();
                exchangeOwner.Release(exchange);
            }
        }

        private readonly INetworkTransport.ChannelPool<Channel> channels;
        private readonly Action<object> cancellationHandler;
        private TimeSpan receiveTimeout;
        private readonly RefAction<Channel, bool> cancellationInvoker;
        private readonly IExchangePool exchanges;

        internal UdpServer(IPEndPoint address, int backlog, MemoryAllocator<byte> allocator, Func<int, IExchangePool> exchangePoolFactory, ILoggerFactory loggerFactory)
            : base(address, backlog, allocator, loggerFactory)
        {
            channels = new INetworkTransport.ChannelPool<Channel>(backlog);
            cancellationHandler = channels.CancellationRequested;
            cancellationInvoker = Channel.Cancel;
            exchanges = exchangePoolFactory(backlog);
        }

        private protected override bool AllowReceiveFromAnyHost => true;

        private protected override void EndReceive(SocketAsyncEventArgs args)
        {
            ReadOnlyMemory<byte> datagram = args.MemoryBuffer.Slice(0, args.BytesTransferred);
            //dispatch datagram to appropriate exchange
            var correlationId = new CorrelationId(ref datagram);
            var headers = new PacketHeaders(ref datagram);
            request_channel:
            if (!channels.TryGetValue(correlationId, out var channel))
                if (exchanges.TryRent(out var exchange)) //channel doesn't exist in the list of active channel but rented successfully
                {
                    channel = new Channel(exchange, exchanges, receiveTimeout, cancellationHandler, correlationId);
                    if (!channels.TryAdd(correlationId, channel))
                    {
                        channel.Dispose();
                        goto request_channel;
                    }
                }
                else
                {
                    logger.NotEnoughRequestHandlers();
                    return;
                }
            if (headers.Control == FlowControl.Cancel)
                ProcessCancellation(cancellationInvoker, ref channel, false, args);   //channel will be removed from the dictionary automatically
            else
                ProcessDatagram(channels, channel, correlationId, headers, datagram, args);
        }

        public new TimeSpan ReceiveTimeout
        {
            get => receiveTimeout;
            set
            {
                base.ReceiveTimeout = (int)value.TotalMilliseconds;
                receiveTimeout = value;
            }
        }

        public void Start()
        {
            Bind(Address);
            base.Start();
        }

        private void Cleanup(bool disposing)
        {
            if (disposing)
            {
                channels.ClearAndDestroyChannels();
                (exchanges as IDisposable)?.Dispose();
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
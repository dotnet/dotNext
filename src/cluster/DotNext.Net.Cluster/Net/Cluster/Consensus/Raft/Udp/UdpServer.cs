using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    using TransportServices;
    using static Runtime.Intrinsics;

    internal sealed class UdpServer : UdpSocket, IServer
    {
        [StructLayout(LayoutKind.Auto)]
        private readonly struct Channel : INetworkTransport.IChannel
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

        private static readonly IPEndPoint AnyRemoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
        private readonly INetworkTransport.ChannelPool<Channel> channels;
        private readonly Action<object> cancellationHandler;
        private TimeSpan receiveTimeout;
        private readonly RefAction<Channel, bool> cancellationInvoker;

        internal UdpServer(IPEndPoint address, int backlog, ArrayPool<byte> bufferPool, ILoggerFactory loggerFactory)
            : base(address, backlog, bufferPool, loggerFactory)
        {
            channels = new INetworkTransport.ChannelPool<Channel>(backlog);
            cancellationHandler = channels.CancellationRequested;
            cancellationInvoker = Channel.Cancel;
        }

        private protected override EndPoint RemoteEndPoint => AnyRemoteEndpoint;

        private protected override void EndReceive(object sender, SocketAsyncEventArgs args)
        {
            var exchangePool = Cast<IExchangePool>(args.UserToken);
            ReadOnlyMemory<byte> datagram = args.MemoryBuffer.Slice(0, args.BytesTransferred);
            //dispatch datagram to appropriate exchange
            var correlationId = new CorrelationId(ref datagram);
            var headers = new PacketHeaders(ref datagram);
            //try rent new exchange
            var exchangeRented = exchangePool.TryRent(headers, out var exchange);
            if(channels.TryGetValue(correlationId, out var channel))
            {
                //return exchange back to the pool
                if(exchangeRented)
                    exchangePool.Release(exchange);
            }
            else if(exchangeRented) //channel doesn't exist in the list of active channel but rented successfully
            {
                var newChannel = new Channel(exchange, exchangePool, receiveTimeout, cancellationHandler, correlationId);
                channel = channels.GetOrAdd(correlationId, newChannel);
                //returned channel is not associated with rented exchange
                //so return exchange back to the pool
                if(!channel.Represents(in newChannel))
                    using(newChannel)
                    {
                        exchangePool.Release(exchange);
                    }
            }
            else
            {
                logger.NotEnoughRequestHandlers();
                return;
            }
            if(headers.Control == FlowControl.Cancel)
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

        public void Start(IExchangePool exchanges)
        {
            Bind(Address);
            base.Start(exchanges);
        }

        private void Cleanup(bool disposing)
        {
            if(disposing)
                channels.ClearAndDestroyChannels();
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
using System.Net;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp;

using Buffers;
using TransportServices;
using TransportServices.Datagram;

internal sealed class UdpServer : UdpSocket, IServer
{
    [StructLayout(LayoutKind.Auto)]
    private readonly struct Channel : IChannel
    {
        private readonly IExchangePool exchangeOwner;
        private readonly IExchange exchange;
        private readonly CancellationTokenSource timeoutTokenSource;
        private readonly CancellationTokenRegistration cancellation;

        internal Channel(IExchange exchange, IExchangePool exchanges, TimeSpan timeout, Action<object?, CancellationToken> cancellationCallback, CorrelationId id, CancellationToken token)
        {
            this.exchange = exchange;
            timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            Token = timeoutTokenSource.Token;
            exchangeOwner = exchanges;
            cancellation = Token.Register(cancellationCallback, id);
            timeoutTokenSource.CancelAfter(timeout);
        }

        IExchange IChannel.Exchange => exchange;

        public CancellationToken Token { get; }

        public void Dispose()
        {
            cancellation.Dispose();
            timeoutTokenSource.Dispose();
            exchangeOwner.Release(exchange);
        }
    }

    private readonly IExchangePool exchanges;
    private readonly ChannelPool<Channel> channels;
    private readonly Action<object?, CancellationToken> cancellationHandler;
    private readonly TimeSpan receiveTimeout;

    internal UdpServer(EndPoint address, int backlog, MemoryAllocator<byte> allocator, Func<int, IExchangePool> exchangePoolFactory, ILoggerFactory loggerFactory)
        : base(address, backlog, allocator, loggerFactory)
    {
        channels = new(backlog);
        cancellationHandler = channels.CancellationRequested;
        exchanges = exchangePoolFactory(backlog);
    }

    private protected override bool AllowReceiveFromAnyHost => true;

    private protected override ValueTask ProcessDatagramAsync(EndPoint ep, CorrelationId id, PacketHeaders headers, ReadOnlyMemory<byte> payload)
    {
        Channel channel;

        while (true)
        {
            if (!channels.TryGetValue(id, out channel))
            {
                // channel doesn't exist in the list of active channel but rented successfully
                if (exchanges.TryRent(out var exchange))
                {
                    channel = new Channel(exchange, exchanges, receiveTimeout, cancellationHandler, id, LifecycleToken);
                    if (!channels.TryAdd(id, channel))
                    {
                        channel.Dispose();
                        continue;
                    }
                }
                else
                {
                    logger.NotEnoughRequestHandlers();
                    return ValueTask.CompletedTask;
                }
            }

            break;
        }

        return ProcessDatagramAsync(ep, channels, channel, id, headers, payload);
    }

    public new TimeSpan ReceiveTimeout
    {
        get => receiveTimeout;
        init
        {
            base.ReceiveTimeout = (int)value.TotalMilliseconds;
            receiveTimeout = value;
        }
    }

    public ValueTask StartAsync(CancellationToken token)
    {
        ValueTask result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled(token);
        }
        else
        {
            result = ValueTask.CompletedTask;
            try
            {
                Bind(Address);
                Start();
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }

        return result;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            channels.ClearAndDestroyChannels(LifecycleToken);
            (exchanges as IDisposable)?.Dispose();
        }
    }

    public ValueTask DisposeAsync()
    {
        var result = ValueTask.CompletedTask;
        try
        {
            Dispose();
        }
        catch (Exception e)
        {
            result = ValueTask.FromException(e);
        }

        return result;
    }
}
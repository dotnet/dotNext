using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp;

using Buffers;
using TransportServices;
using static Threading.AtomicInt64;

internal sealed class UdpClient : UdpSocket, IClient
{
    [StructLayout(LayoutKind.Auto)]
    private readonly struct Channel : INetworkTransport.IChannel
    {
        private readonly IExchange exchange;
        private readonly CancellationTokenSource tokenSource;
        private readonly CancellationTokenRegistration cancellation;

        internal Channel(IExchange exchange, Action<object?, CancellationToken> cancellationCallback, CorrelationId id, CancellationToken token1, CancellationToken token2)
        {
            this.exchange = exchange;
            tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token1, token2);
            Token = tokenSource.Token;
            cancellation = Token.IsCancellationRequested ? default : Token.Register(cancellationCallback, id);
        }

        public CancellationToken Token { get; }

        IExchange INetworkTransport.IChannel.Exchange => exchange;

        internal void Complete(Exception e) => exchange.OnException(e);

        public void Dispose()
        {
            cancellation.Dispose();
            tokenSource.Dispose();
        }
    }

    private readonly Action<object?, CancellationToken> cancellationHandler;

    // I/O management
    private readonly long applicationId;
    private readonly INetworkTransport.ChannelPool<Channel> channels;
    private readonly IPEndPoint localEndPoint;
    private long streamNumber;

    internal UdpClient(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, int backlog, MemoryAllocator<byte> allocator, Func<long> appIdGenerator, ILoggerFactory loggerFactory)
        : base(remoteEndPoint, backlog, allocator, loggerFactory)
    {
        channels = new INetworkTransport.ChannelPool<Channel>(backlog);
        cancellationHandler = channels.CancellationRequested;
        applicationId = appIdGenerator();
        streamNumber = long.MinValue;
        this.localEndPoint = localEndPoint;
    }

    ValueTask IClient.CancelPendingRequestsAsync()
    {
        channels.ClearAndDestroyChannels();
        return new();
    }

    private protected override ValueTask ProcessDatagramAsync(EndPoint ep, CorrelationId id, PacketHeaders headers, ReadOnlyMemory<byte> payload)
    {
        // dispatch datagram to appropriate exchange
        if (channels.TryGetValue(id, out var channel))
            return ProcessDatagramAsync(ep, channels, channel, id, headers, payload);

        logger.PacketDropped(id, ep);
        return ValueTask.CompletedTask;
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
        var channel = new Channel(exchange, cancellationHandler, id, token, LifecycleToken);
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
            channels.ClearAndDestroyChannels(LifecycleToken);
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
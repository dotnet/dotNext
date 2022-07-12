using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp;

using Buffers;
using TransportServices;
using TransportServices.Datagram;

internal abstract class UdpSocket : Socket, INetworkTransport
{
    internal const int MaxDatagramSize = 65507;
    internal const int MinDatagramSize = 300;
    private static readonly IPEndPoint AnyRemoteEndpoint = new(IPAddress.Any, 0);

    private protected readonly MemoryAllocator<byte> allocator;
    internal readonly EndPoint Address;
    private protected readonly ILogger logger;
    private readonly int listeners;

    // I/O management
    private readonly int datagramSize;

    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private volatile CancellationTokenSource? lifecycleControl;

    private protected UdpSocket(EndPoint address, int backlog, MemoryAllocator<byte> allocator, ILoggerFactory loggerFactory)
        : base(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp)
    {
        ExclusiveAddressUse = true;
        Blocking = false;
        Address = address;
        logger = loggerFactory.CreateLogger(GetType());
        this.allocator = allocator;
        lifecycleControl = new();
        LifecycleToken = lifecycleControl.Token;
        datagramSize = MinDatagramSize;
        listeners = backlog;
    }

    private protected CancellationToken LifecycleToken { get; } // cached to avoid ObjectDisposedException

    EndPoint INetworkTransport.Address => Address;

    internal static int ValidateDatagramSize(int value)
        => value.IsBetween(MinDatagramSize, MaxDatagramSize, BoundType.Closed) ? value : throw new ArgumentOutOfRangeException(nameof(value));

    internal int DatagramSize
    {
        get => datagramSize;
        init => datagramSize = ValidateDatagramSize(value);
    }

    private protected abstract bool AllowReceiveFromAnyHost { get; }

    private protected void Start()
    {
        for (var i = 0; i < listeners; i++)
            ThreadPool.QueueUserWorkItem<UdpSocket>(static socket => socket.ListenerLoopAsync(), this, false);
    }

    private async void ListenerLoopAsync()
    {
        using var buffer = AllocDatagramBuffer();

        for (var pending = true; pending && !LifecycleToken.IsCancellationRequested; )
        {
            try
            {
                var result = await ReceiveFromAsync(buffer.Memory, SocketFlags.None, AllowReceiveFromAnyHost ? AnyRemoteEndpoint : Address, LifecycleToken).ConfigureAwait(false);
                ReadOnlyMemory<byte> datagram = buffer.Memory.Slice(0, result.ReceivedBytes);

                datagram = datagram.Slice(ParseDatagram(datagram.Span, out var id, out var headers));
                await ProcessDatagramAsync(result.RemoteEndPoint, id, headers, datagram).ConfigureAwait(false);
            }
            catch (Exception e) when (e is OperationCanceledException || e is ObjectDisposedException)
            {
                pending = false;
            }
            catch (SocketException e)
            {
                switch (e.SocketErrorCode)
                {
                    case SocketError.OperationAborted:
                    case SocketError.Shutdown:
                        pending = false;
                        break;
                    default:
                        logger.SockerErrorOccurred(e.SocketErrorCode);
                        break;
                }
            }
            catch (Exception e)
            {
                logger.SocketAcceptLoopTerminated(e);
            }
        }

        static int ParseDatagram(ReadOnlySpan<byte> datagram, out CorrelationId id, out PacketHeaders headers)
        {
            var reader = new SpanReader<byte>(datagram);

            id = new(ref reader);
            headers = new(ref reader);

            return reader.ConsumedCount;
        }
    }

    private protected abstract ValueTask ProcessDatagramAsync(EndPoint ep, CorrelationId id, PacketHeaders headers, ReadOnlyMemory<byte> payload);

    private protected async ValueTask ProcessDatagramAsync<TChannel>(EndPoint ep, ConcurrentDictionary<CorrelationId, TChannel> channels, TChannel channel, CorrelationId correlationId, PacketHeaders headers, ReadOnlyMemory<byte> datagram)
        where TChannel : struct, IChannel
    {
        bool stateFlag;
        var error = default(Exception);
        Debug.Assert(ep is not null);

        // handle received packet
        try
        {
            stateFlag = await channel.Exchange.ProcessInboundMessageAsync(headers, datagram, channel.Token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            stateFlag = false;
            error = e;
        }

        // send one more datagram if exchange requires this
        if (stateFlag)
        {
            try
            {
                stateFlag = await SendAsync(correlationId, channel, ep).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                stateFlag = false;
                error = e;
            }
        }

        // remove exchange if it is in final state
        if (!stateFlag && channels.TryRemove(correlationId, out channel))
        {
            using (channel)
            {
                if (error is not null)
                    channel.Exchange.OnException(error);
            }
        }
    }

    private protected async ValueTask<bool> SendAsync<TChannel>(CorrelationId id, TChannel channel, EndPoint endpoint)
        where TChannel : struct, IChannel
    {
        bool waitForInput;
        var bufferHolder = AllocDatagramBuffer();
        try
        {
            PacketHeaders headers;
            int bytesWritten;

            // write payload
            (headers, bytesWritten, waitForInput) = await channel.Exchange.CreateOutboundMessageAsync(AdjustToPayload(bufferHolder.Memory), channel.Token).ConfigureAwait(false);

            // write correlation ID and headers
            var prologueSize = WritePrologue(bufferHolder.Span, in id, headers);
            await SendToAsync(bufferHolder.Memory.Slice(0, prologueSize + bytesWritten), endpoint, channel.Token).ConfigureAwait(false);
        }
        finally
        {
            bufferHolder.Dispose();
        }

        return waitForInput;

        static int WritePrologue(Span<byte> buffer, in CorrelationId id, PacketHeaders headers)
        {
            var writer = new SpanWriter<byte>(buffer);

            id.Format(ref writer);
            headers.Format(ref writer);

            return writer.WrittenCount;
        }
    }

    private async ValueTask SendToAsync(ReadOnlyMemory<byte> datagram, EndPoint endPoint, CancellationToken token)
    {
        for (int bytesWritten; !datagram.IsEmpty; datagram = datagram.Slice(bytesWritten))
        {
            bytesWritten = await SendToAsync(datagram, SocketFlags.None, endPoint, token).ConfigureAwait(false);
        }
    }

    private protected MemoryOwner<byte> AllocDatagramBuffer()
        => allocator(datagramSize);

    private protected static Memory<byte> AdjustToPayload(Memory<byte> packet)
        => packet.Slice(PacketHeaders.Size + CorrelationId.Size);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            var tokenSource = Interlocked.Exchange(ref lifecycleControl, null);
            try
            {
                tokenSource?.Cancel(false);
            }
            finally
            {
                tokenSource?.Dispose();
            }
        }

        base.Dispose(disposing);
    }
}
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.Tcp;

using Buffers;
using TransportServices;
using TransportServices.ConnectionOriented;

internal sealed class TcpClient : Client, ITcpTransport
{
    private sealed class ConnectionContext : Disposable, IConnectionContext
    {
        private readonly TcpStream transport;
        private readonly TcpProtocolStream protocol;
        private MemoryOwner<byte> buffer;

        internal ConnectionContext(TcpStream transport, TcpProtocolStream protocol, int bufferSize, MemoryAllocator<byte> allocator)
        {
            Debug.Assert(transport is not null);
            Debug.Assert(protocol is not null);

            this.transport = transport;
            this.protocol = protocol;
            buffer = allocator.Invoke(bufferSize, exactSize: false);
        }

        internal int CloseTimeout
        {
            get;
            init;
        }

        ProtocolStream IConnectionContext.Protocol => protocol;

        Memory<byte> IConnectionContext.Buffer => buffer.Memory;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                protocol.Dispose();
                transport.Dispose();
            }

            buffer.Dispose();
            base.Dispose(disposing);
        }

        protected override async ValueTask DisposeAsyncCore()
        {
            try
            {
                if (protocol?.BaseStream is SslStream ssl)
                {
                    using (ssl)
                        await ssl.ShutdownAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                protocol.Dispose();
                transport.Close(CloseTimeout);
                transport.Dispose();
            }
        }

        public new ValueTask DisposeAsync() => base.DisposeAsync();
    }

    private readonly MemoryAllocator<byte> allocator;
    private readonly int transmissionBlockSize;
    private readonly byte ttl;
    private readonly LingerOption linger;

    internal TcpClient(ILocalMember localMember, EndPoint endPoint, MemoryAllocator<byte> allocator)
        : base(localMember, endPoint)
    {
        Debug.Assert(allocator is not null);

        this.allocator = allocator;
        transmissionBlockSize = ITcpTransport.MinTransmissionBlockSize;
        ttl = ITcpTransport.DefaultTtl;
        linger = ITcpTransport.CreateDefaultLingerOption();
    }

    public SslClientAuthenticationOptions? SslOptions
    {
        get;
        init;
    }

    public int TransmissionBlockSize
    {
        get => transmissionBlockSize;
        init => transmissionBlockSize = ITcpTransport.ValidateTranmissionBlockSize(value);
    }

    public byte Ttl
    {
        get => ttl;
        init => ttl = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    public LingerOption LingerOption
    {
        get => linger;
        init => linger = value ?? throw new ArgumentNullException(nameof(value));
    }

    EndPoint INetworkTransport.Address => EndPoint;

    private protected override async ValueTask<IConnectionContext> ConnectAsync(CancellationToken token)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);

        // connection has separated timeout
        var connectDurationTracker = CancellationTokenSource.CreateLinkedTokenSource(token);
        try
        {
            connectDurationTracker.CancelAfter(ConnectTimeout);
            await socket.ConnectAsync(EndPoint, token).ConfigureAwait(false);
        }
        catch
        {
            socket.Dispose();
            connectDurationTracker.Dispose();
            throw;
        }

        ITcpTransport.ConfigureSocket(socket, linger, ttl);
        var transport = new TcpStream(socket, owns: true);
        transport.WriteTimeout = (int)RequestTimeout.TotalMilliseconds;
        TcpProtocolStream protocol;
        if (SslOptions is null)
        {
            protocol = new(transport, allocator, transmissionBlockSize);
            connectDurationTracker.Dispose();
        }
        else
        {
            var ssl = new SslStream(transport, leaveInnerStreamOpen: true);

            try
            {
                await ssl.AuthenticateAsClientAsync(SslOptions, connectDurationTracker.Token).ConfigureAwait(false);
            }
            catch
            {
                await transport.DisposeAsync().ConfigureAwait(false);
                await ssl.DisposeAsync().ConfigureAwait(false);
                transport = null;
                throw;
            }
            finally
            {
                connectDurationTracker.Dispose();
            }

            protocol = new(ssl, allocator, transmissionBlockSize);
        }

        return new ConnectionContext(transport, protocol, transmissionBlockSize, allocator)
        {
            CloseTimeout = (int)RequestTimeout.TotalMilliseconds,
        };
    }
}
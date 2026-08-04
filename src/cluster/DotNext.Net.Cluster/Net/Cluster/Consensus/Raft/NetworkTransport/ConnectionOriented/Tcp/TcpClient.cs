using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.NetworkTransport.ConnectionOriented.Tcp;

using Buffers;

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
            buffer = allocator.AllocateAtLeast(bufferSize);
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
                if (protocol.BaseStream is SslStream ssl)
                {
                    try
                    {
                        await ssl.ShutdownAsync().ConfigureAwait(false);
                    }
                    finally
                    {
                        await ssl.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                await protocol.DisposeAsync().ConfigureAwait(false);
                transport.Close(CloseTimeout);
                await transport.DisposeAsync().ConfigureAwait(false);
            }
        }

        public new ValueTask DisposeAsync() => base.DisposeAsync();
    }

    private readonly int transmissionBlockSize;
    private readonly byte ttl;
    private readonly LingerOption linger;

    internal TcpClient(ILocalMember localMember, EndPoint endPoint)
        : base(localMember, endPoint)
    {
        transmissionBlockSize = ITcpTransport.MinTransmissionBlockSize;
        ttl = ITcpTransport.DefaultTtl;
        linger = ITcpTransport.CreateDefaultLingerOption();
    }
    
    public required MemoryAllocator<byte> MemoryAllocator { get; init; }

    public SslClientAuthenticationOptions? SslOptions
    {
        get;
        init;
    }

    public int TransmissionBlockSize
    {
        get => transmissionBlockSize;
        init => transmissionBlockSize = ITcpTransport.ValidateTransmissionBlockSize(value);
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
        var connectDurationTracker = CombineTokens(ConnectTimeout, token);
        try
        {
            await socket.ConnectAsync(EndPoint, connectDurationTracker.Token).ConfigureAwait(false);
        }
        catch
        {
            socket.Dispose();
            await connectDurationTracker.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        ITcpTransport.ConfigureSocket(socket, linger, ttl);
        var transport = new TcpStream(socket, owns: true)
        {
            WriteTimeout = (int)RequestTimeout.TotalMilliseconds
        };

        TcpProtocolStream protocol;
        if (SslOptions is null)
        {
            protocol = new(transport, MemoryAllocator, transmissionBlockSize);
            await connectDurationTracker.DisposeAsync().ConfigureAwait(false);
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
                throw;
            }
            finally
            {
                await connectDurationTracker.DisposeAsync().ConfigureAwait(false);
            }

            protocol = new(ssl, MemoryAllocator, transmissionBlockSize);
        }

        return new ConnectionContext(transport, protocol, transmissionBlockSize, MemoryAllocator)
        {
            CloseTimeout = (int)RequestTimeout.TotalMilliseconds,
        };
    }
}
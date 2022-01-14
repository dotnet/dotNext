using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.Tcp;

using Buffers;
using TransportServices;
using static IO.StreamExtensions;

internal abstract class TcpTransport : Disposable, INetworkTransport
{
    private protected class PacketStream : Disposable
    {
        private readonly TcpStream transport;

        // actual stream for Network I/O
        // can be of type TcpStream or SslStream
        private readonly Stream networkStream;
        private protected readonly SslStream? ssl;

        internal PacketStream(Socket socket, bool owns, bool useSsl)
        {
            transport = new TcpStream(socket, owns);
            if (useSsl)
            {
                ssl = new SslStream(transport, true);
                networkStream = ssl;
            }
            else
            {
                ssl = null;
                networkStream = transport;
            }
        }

        internal bool Connected => transport.Connected;

        private protected EndPoint? RemoteEndPoint => transport.RemoteEndPoint;

        internal void Close(int timeout) => transport.Close(timeout);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ssl?.Dispose();
                transport.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private const int MinTransmissionBlockSize = 300;

    internal readonly IPEndPoint Address;
    private protected readonly ILogger logger;
    private readonly MemoryAllocator<byte> allocator;
    private int transmissionBlockSize;

    private protected TcpTransport(IPEndPoint address, MemoryAllocator<byte> allocator, ILoggerFactory loggerFactory)
    {
        Address = address;
        logger = loggerFactory.CreateLogger(GetType());
        this.allocator = allocator;
        transmissionBlockSize = MinTransmissionBlockSize;
        LingerOption = new LingerOption(false, 0);
        Ttl = 64;
    }

    private protected static void ConfigureSocket(Socket socket, LingerOption linger, byte ttl)
    {
        socket.NoDelay = true;
        socket.Ttl = ttl;
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, linger);
    }

    internal static int ValidateTranmissionBlockSize(int value)
        => value >= MinTransmissionBlockSize ? value : throw new ArgumentOutOfRangeException(nameof(value));

    internal int TransmissionBlockSize
    {
        get => transmissionBlockSize;
        set => transmissionBlockSize = ValidateTranmissionBlockSize(value);
    }

    internal LingerOption LingerOption
    {
        get;
        set;
    }

    internal byte Ttl
    {
        get;
        set;
    }

    IPEndPoint INetworkTransport.Address => Address;
}
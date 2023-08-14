using System.Net.Sockets;

namespace DotNext.Net.Cluster.Consensus.Raft.Tcp;

using TransportServices;

internal interface ITcpTransport : INetworkTransport
{
    private protected const int MinTransmissionBlockSize = 300;
    private protected const int DefaultTtl = 64;

    internal static int ValidateTransmissionBlockSize(int value)
        => value >= MinTransmissionBlockSize ? value : throw new ArgumentOutOfRangeException(nameof(value));

    int TransmissionBlockSize { get; init; }

    LingerOption LingerOption { get; init; }

    byte Ttl { get; init; }

    private protected static void ConfigureSocket(Socket socket, LingerOption linger, byte ttl)
    {
        socket.NoDelay = true;
        socket.Ttl = ttl;
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, linger);
    }

    internal static LingerOption CreateDefaultLingerOption() => new(false, 0);
}
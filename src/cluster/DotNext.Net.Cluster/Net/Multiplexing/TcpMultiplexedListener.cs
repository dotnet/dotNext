using System.Net;
using System.Net.Sockets;

namespace DotNext.Net.Multiplexing;

/// <summary>
/// Represents a server-side of the multiplexing protocol on top of TCP.
/// </summary>
/// <param name="listenAddress"></param>
/// <param name="configuration"></param>
public class TcpMultiplexedListener(EndPoint listenAddress, MultiplexedListener.Options configuration) : MultiplexedListener(configuration)
{
    private readonly int backlog = configuration.Backlog;
    
    /// <inheritdoc/>
    protected sealed override Socket Listen()
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(listenAddress);
        socket.Listen(backlog);
        return socket;
    }
}
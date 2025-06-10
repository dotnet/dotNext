using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;

namespace DotNext.Net.Multiplexing;

public class TcpMultiplexedClient(EndPoint address, int fragmentSize = 1380, PipeOptions? options = null) : MultiplexedClient(fragmentSize, options)
{
    protected override async ValueTask<Socket> ConnectAsync(CancellationToken token)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(address, token).ConfigureAwait(false);
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        return socket;
    }
}
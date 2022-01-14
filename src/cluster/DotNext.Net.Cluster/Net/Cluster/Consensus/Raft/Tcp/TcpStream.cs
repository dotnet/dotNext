using System.Net;
using System.Net.Sockets;

namespace DotNext.Net.Cluster.Consensus.Raft.Tcp;

internal sealed class TcpStream : NetworkStream
{
    internal TcpStream(Socket socket, bool owns)
        : base(socket, owns)
    {
    }

    internal bool Connected => Socket.Connected;

    internal EndPoint? RemoteEndPoint => Socket.RemoteEndPoint;
}
using System;
using System.Net.Http;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class RaftClientHandlerFactory : IHttpMessageHandlerFactory
    {
        public HttpMessageHandler CreateHandler(string name) => new SocketsHttpHandler {ConnectTimeout = TimeSpan.FromMilliseconds(100)};
    }
}

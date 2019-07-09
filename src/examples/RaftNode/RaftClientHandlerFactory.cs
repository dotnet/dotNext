using System;
using System.Net.Http;

namespace RaftNode
{
    internal sealed class RaftClientHandlerFactory : IHttpMessageHandlerFactory
    {
        public HttpMessageHandler CreateHandler(string name) => new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromMilliseconds(100) };
    }
}

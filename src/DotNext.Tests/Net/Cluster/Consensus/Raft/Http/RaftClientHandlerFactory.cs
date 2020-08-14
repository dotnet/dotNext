using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    [ExcludeFromCodeCoverage]
    internal sealed class RaftClientHandlerFactory : IHttpMessageHandlerFactory
    {
        public HttpMessageHandler CreateHandler(string name) => new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromMilliseconds(100) };
    }
}

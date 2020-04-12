using System;
using System.Net;
using System.Threading;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    internal interface INetworkTransport : IDisposable
    {
        private protected interface IChannel : IDisposable
        {
            IExchange Exchange { get; }
            CancellationToken Token { get; }
        }

        IPEndPoint Address { get; }
    }
}
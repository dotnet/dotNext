using System;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    internal interface IServer : INetworkTransport
    {
        TimeSpan ReceiveTimeout { get; }

        void Start(IExchangePool pool);
    }
}
using System;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    internal interface IReusableExchange : IExchange, IDisposable
    {
        void Reset();
    }
}
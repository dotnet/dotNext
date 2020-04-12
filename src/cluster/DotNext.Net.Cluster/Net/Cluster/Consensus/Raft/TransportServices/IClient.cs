using System.Threading;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    internal interface IClient : INetworkTransport
    {
        void Start();

        void Enqueue<TExchange>(TExchange exchange, CancellationToken token)
            where TExchange : class, IExchange;
    }
}
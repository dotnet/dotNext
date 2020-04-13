using System.Threading;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    /// <summary>
    /// Represents client-side of the network transport.
    /// </summary>
    internal interface IClient : INetworkTransport
    {
        void Enqueue<TExchange>(TExchange exchange, CancellationToken token)
            where TExchange : class, IExchange;

        void CancelPendingRequests();
    }
}
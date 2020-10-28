using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    /// <summary>
    /// Represents client-side of the network transport.
    /// </summary>
    internal interface IClient : INetworkTransport
    {
        void Enqueue(IExchange exchange, CancellationToken token);

        ValueTask CancelPendingRequestsAsync();
    }
}
using System.Net;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

internal interface INetworkTransport : IDisposable
{
    EndPoint Address { get; }
}
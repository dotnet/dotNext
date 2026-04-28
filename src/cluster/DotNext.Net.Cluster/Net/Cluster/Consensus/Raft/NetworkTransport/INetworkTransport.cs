using System.Net;

namespace DotNext.Net.Cluster.Consensus.Raft.NetworkTransport;

internal interface INetworkTransport : IDisposable
{
    EndPoint Address { get; }
}
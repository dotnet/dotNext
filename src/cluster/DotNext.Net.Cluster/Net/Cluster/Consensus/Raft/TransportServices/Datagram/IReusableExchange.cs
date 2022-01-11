namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

internal interface IReusableExchange : IExchange
{
    void Reset();
}
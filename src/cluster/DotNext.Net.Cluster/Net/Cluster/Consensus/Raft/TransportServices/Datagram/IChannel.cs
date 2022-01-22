namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.Datagram;

/// <summary>
/// Represents logical communication channel inside of physical connection.
/// </summary>
internal interface IChannel : IDisposable
{
    IExchange Exchange { get; }

    CancellationToken Token { get; }
}
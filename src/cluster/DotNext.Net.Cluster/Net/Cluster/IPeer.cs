using System.Net;

namespace DotNext.Net.Cluster
{
    /// <summary>
    /// Represents peer in network communication.
    /// </summary>
    public interface IPeer
    {
        /// <summary>
        /// The address of the peer.
        /// </summary>
        EndPoint EndPoint { get; }
    }
}
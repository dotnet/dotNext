using System.Net;

namespace DotNext.Net
{
    /// <summary>
    /// Represents a peer in network communication.
    /// </summary>
    public interface IPeer
    {
        /// <summary>
        /// The address of the peer.
        /// </summary>
        EndPoint EndPoint { get; }
    }
}
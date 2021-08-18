using System.Collections.Generic;
using System.Net;

namespace DotNext.Net
{
    /// <summary>
    /// Provides local view of peer mesh. 
    /// </summary>
    public interface IPeerMesh
    {
        /// <summary>
        /// Gets a collection of visible peers.
        /// </summary>
        IReadOnlyCollection<EndPoint> Peers { get; } // TODO: Use IReadOnlySet in .NET 6
    }

    /// <summary>
    /// Provides local view of peer mesh. 
    /// </summary>
    public interface IPeerMesh<out TPeer> : IPeerMesh
        where TPeer : IPeer
    {
        /// <summary>
        /// Gets a client used to communucate with remote peer.
        /// </summary>
        /// <param name="peer">The address of the peer.</param>
        /// <returns>The peer client.</returns>
        /// <exception cref="InvalidPeerAddressException"><paramref name="peer"/> is not reachable from the current peer.</exception>
        TPeer GetPeer(EndPoint peer);
    }
}
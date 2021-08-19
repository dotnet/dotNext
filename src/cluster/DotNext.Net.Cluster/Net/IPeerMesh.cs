using System;
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
        where TPeer : class, IPeer
    {
        /// <summary>
        /// Gets a client used to communucate with remote peer.
        /// </summary>
        /// <param name="peer">The address of the peer.</param>
        /// <returns>The peer client; or <see langword="null"/> if the specified peer is not visible from the current peer.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="peer"/> is <see langword="null"/>.</exception>
        TPeer? TryGetPeer(EndPoint peer);
    }
}
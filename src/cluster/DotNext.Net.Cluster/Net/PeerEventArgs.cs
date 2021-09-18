using System.Net;

namespace DotNext.Net;

/// <summary>
/// Represents arguments of <see cref="IPeerMesh.PeerDiscovered"/>
/// and <see cref="IPeerMesh.PeerGone"/> events.
/// </summary>
public abstract class PeerEventArgs : EventArgs
{
    /// <summary>
    /// Gets the address of the peer.
    /// </summary>
    public abstract EndPoint PeerAddress { get; }

    /// <summary>
    /// Creates a new instance of <see cref="PeerEventArgs"/> class.
    /// </summary>
    /// <param name="peer">The peer address.</param>
    /// <returns>A new instance of <see cref="PeerEventArgs"/> class.</returns>
    public static PeerEventArgs Create(EndPoint peer) => new SimplePeerEventArgs(peer);
}

internal sealed class SimplePeerEventArgs : PeerEventArgs
{
    internal SimplePeerEventArgs(EndPoint peer) => PeerAddress = peer ?? throw new ArgumentNullException(nameof(peer));

    public override EndPoint PeerAddress { get; }
}
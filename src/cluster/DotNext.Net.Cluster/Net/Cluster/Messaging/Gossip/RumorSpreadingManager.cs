using System.Collections.Concurrent;
using System.Net;

namespace DotNext.Net.Cluster.Messaging.Gossip;

/// <summary>
/// Represents a helper that allows to control spreading of the rumor (send/receive)
/// using Lamport timestamps.
/// </summary>
/// <remarks>
/// Any instance members of this class are thread-safe.
/// This class helps to organize rumour spreading across peers in the network.
/// When the peer acts as a source of the rumour (sender), it should call <see cref="Tick()"/>
/// method to obtain a Lamport timestamp for the message. Then it attaches the address
/// of the local peer to the rumour. Receiver calls
/// <see cref="CheckOrder(EndPoint, in RumorTimestamp)"/> method
/// to check the message order correctness. If the method returns <see langword="true"/>
/// the receiver processes the message and retransmits it to other peers using the algorithm
/// described for the sender (original address and id remain intact).
/// If the method returns <see langword="false"/> then the receiver must skip the message
/// and prevent its retransmission.
/// </remarks>
public sealed class RumorSpreadingManager
{
    private readonly ConcurrentDictionary<EndPoint, RumorTimestamp> state;
    private RumorTimestamp currentTimestamp;

    /// <summary>
    /// Initializes a new manager.
    /// </summary>
    /// <param name="addressComparer">The peer comparison algorithm.</param>
    public RumorSpreadingManager(IEqualityComparer<EndPoint>? addressComparer = null)
    {
        state = new(addressComparer);
        currentTimestamp = new();
    }

    /// <summary>
    /// Advances the logical timer.
    /// </summary>
    /// <remarks>
    /// This method is typically called by the source of the rumour.
    /// </remarks>
    /// <returns>The monotonically increasing local timer value.</returns>
    public RumorTimestamp Tick() => RumorTimestamp.Next(ref currentTimestamp);

    /// <summary>
    /// Checks whether the received rumour should be processed by the local peer
    /// and retransmitted to other peers.
    /// </summary>
    /// <param name="origin">The address of the sender.</param>
    /// <param name="timestamp">The rumor timestamp.</param>
    /// <returns>
    /// <see langword="true"/> if the message is allowed for processing;
    /// <see langword="false"/> if the message must be skipped.
    /// </returns>
    public bool CheckOrder(EndPoint origin, in RumorTimestamp timestamp)
    {
        while (state.TryGetValue(origin, out var currentTs) && timestamp >= currentTs)
        {
            // attempts to update atomically
            if (state.TryUpdate(origin, timestamp.Increment(), currentTs))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to enable message order control for the specified peer.
    /// </summary>
    /// <remarks>
    /// Without calling of this method, <see cref="CheckOrder"/> rejects any message
    /// from the particular sender.
    /// This method can be used as a reaction on <see cref="IPeerMesh.PeerDiscovered"/> event.
    /// </remarks>
    /// <param name="peerAddress">The address of the peer.</param>
    /// <returns>
    /// <see langword="true"/> if the peer is added to this manager successfully;
    /// <see langword="false"/> if the peer is already tracking by this manager.
    /// </returns>
    public bool TryEnableControl(EndPoint peerAddress)
        => state.TryAdd(peerAddress, RumorTimestamp.MinValue);

    /// <summary>
    /// Attempts to disable message order control for the specified peer.
    /// </summary>
    /// <remarks>
    /// This method can be used as a reaction on <see cref="IPeerMesh.PeerGone"/> event.
    /// </remarks>
    /// <param name="peerAddress">The address of the peer.</param>
    /// <returns><see langword="true"/> if the tracking for the specified endpoint disabled successfully; otherwise, <see langword="false"/>.</returns>
    public bool TryDisableControl(EndPoint peerAddress)
        => state.TryRemove(peerAddress, out _);
}
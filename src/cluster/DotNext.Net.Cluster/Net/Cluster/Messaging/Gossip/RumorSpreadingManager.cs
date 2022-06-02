using System.Collections.Concurrent;
using System.Net;

namespace DotNext.Net.Cluster.Messaging.Gossip;

using static Threading.AtomicInt64;

/// <summary>
/// Represents a helper that allows to control spreading of the rumor (send/receive)
/// using Lamport timestamps.
/// </summary>
/// <remarks>
/// Any instance members of this class are thread-safe.
/// This class helps to organize rumour spreading across peers in the network.
/// When the peer acts as a source of the rumour (sender), it should call <see cref="Tick()"/>
/// method to obtain a Lamport timestamp for the message. Then it attaches the address
/// of the local peer and <see cref="LocalId"/> to the rumour. Receiver calls
/// <see cref="CheckMessageOrder(EndPoint, in PeerTransientId, long)"/> method
/// to check the message order correctness. If the method returns <see langword="true"/>
/// the receiver processes the message and retransmits it to other peers using the algorithm
/// described for the sender.
/// If the method returns <see langword="false"/> then the receiver must skip the message
/// and prevent its retransmission.
/// </remarks>
public sealed class RumorSpreadingManager
{
    private readonly record struct PeerState
    {
        internal PeerTransientId Id { get; init; }
        internal long Counter { get; init; }
    }

    private readonly PeerTransientId peerTransientId;
    private readonly ConcurrentDictionary<EndPoint, PeerState> state;
    private long counter;

    /// <summary>
    /// Initializes a new manager.
    /// </summary>
    /// <param name="addressComparer"></param>
    public RumorSpreadingManager(IEqualityComparer<EndPoint>? addressComparer = null)
    {
        peerTransientId = new(Random.Shared);
        counter = long.MinValue;
        state = new(addressComparer);
    }

    /// <summary>
    /// Gets 16-bytes long unique identifier of the local peer.
    /// </summary>
    /// <remarks>
    /// This identifier doesn't survive the application restart even if the adress
    /// of the current peer remains the same.
    /// </remarks>
    public ref readonly PeerTransientId LocalId => ref peerTransientId;

    /// <summary>
    /// Advances the logical timer.
    /// </summary>
    /// <remarks>
    /// This method is typically called by the source of the rumour.
    /// </remarks>
    /// <returns>The monotonically increasing local timer value.</returns>
    public long Tick() => counter.IncrementAndGet();

    /// <summary>
    /// Checks whether the received rumour should be processed by the local peer
    /// and retransmitted to other peers.
    /// </summary>
    /// <param name="sender">The address of the sender.</param>
    /// <param name="senderId">The transient identifier of the sender.</param>
    /// <param name="senderCounter">The message counter.</param>
    /// <returns>
    /// <see langword="true"/> if the message is allowed for processing;
    /// <see langword="false"/> if the message must be skipped.
    /// </returns>
    public bool CheckMessageOrder(EndPoint sender, in PeerTransientId senderId, long senderCounter)
    {
        PeerState newInfo;
        while (state.TryGetValue(sender, out var currentInfo))
        {
            if (senderId.Equals(currentInfo.Id))
            {
                // receiving older message
                if (senderCounter <= currentInfo.Counter)
                    break;

                // update counter
                newInfo = currentInfo with { Counter = senderCounter };
            }
            else if (senderId.CreatedAt >= currentInfo.Id.CreatedAt)
            {
                // sender found, but IDs are different. It means that the peer had restarted
                newInfo = new() { Id = senderId, Counter = senderCounter };
            }
            else
            {
                // received delated message created by previous 'version' of the sender
                break;
            }

            // attempts to update atomically
            if (state.TryUpdate(sender, newInfo, currentInfo))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to enable message order control for the specified peer.
    /// </summary>
    /// <remarks>
    /// Without calling of this method, <see cref="CheckMessageOrder"/> rejects any message
    /// from the particular sender.
    /// This method can be used as a reaction on <see cref="IPeerMesh.PeerDiscovered"/> event.
    /// </remarks>
    /// <param name="peerAddress">The address of the peer.</param>
    /// <returns>
    /// <see langword="true"/> if the peer is added to this manager successfully;
    /// <see langword="false"/> if the peer is already tracking by this manager.
    /// </returns>
    public bool TryEnableMessageOrderControl(EndPoint peerAddress)
        => state.TryAdd(peerAddress, new() { Counter = long.MinValue });

    /// <summary>
    /// Attempts to disable message order control for the specified peer.
    /// </summary>
    /// <remarks>
    /// This method can be used as a reaction on <see cref="IPeerMesh.PeerGone"/> event.
    /// </remarks>
    /// <param name="peerAddress"></param>
    /// <returns></returns>
    public bool TryDisableMessageOrderControl(EndPoint peerAddress)
        => state.TryRemove(peerAddress, out _);
}
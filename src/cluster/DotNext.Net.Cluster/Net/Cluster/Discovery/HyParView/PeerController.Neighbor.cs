using System.Net;

namespace DotNext.Net.Cluster.Discovery.HyParView;

public partial class PeerController
{
    /// <summary>
    /// Sends Neighbor message to the specified peer.
    /// </summary>
    /// <param name="neighbor">The receiver of the message.</param>
    /// <param name="highPriority">
    /// <see langword="true"/> to add the peer to the active view of receiver even if view
    /// is full; otherwise, <see langword="false"/>.
    /// </param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result of the operation.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    protected abstract Task NeighborAsync(EndPoint neighbor, bool highPriority, CancellationToken token);

    /// <summary>
    /// Must be called by transport layer when Neighbor request is received.
    /// </summary>
    /// <param name="sender">The announcement of the neighbor peer.</param>
    /// <param name="highPriority">
    /// <see langword="true"/> to replace another peer from the current active view with the announced peer;
    /// <see langword="false"/> to place the announced peer to the current passive view if active view is full.
    /// </param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The controller has been disposed.</exception>
    protected ValueTask EnqueueNeighborAsync(EndPoint sender, bool highPriority, CancellationToken token = default)
        => IsDisposed ? new(DisposedTask) : EnqueueAsync(Command.Neighbor(sender, highPriority), token);

    private Task ProcessNeighborAsync(EndPoint neighbor, bool highPriority)
        => highPriority || activeView.Count < activeViewCapacity ? AddPeerToActiveViewAsync(neighbor, highPriority) : Task.CompletedTask;
}
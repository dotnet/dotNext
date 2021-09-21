using System.Net;

namespace DotNext.Net.Cluster.Discovery.HyParView;

using Buffers;

public partial class PeerController
{
    /// <summary>
    /// Sends Join request to contact node.
    /// </summary>
    /// <param name="contactNode">The address of the contact node.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing communication operation.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    protected abstract Task JoinAsync(EndPoint contactNode, CancellationToken token);

    /// <summary>
    /// Must be called by transport layer when Join request is received.
    /// </summary>
    /// <param name="joinedPeer">The joined peer.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The controller has been disposed.</exception>
#if DEBUG
    internal
#endif
    protected ValueTask EnqueueJoinAsync(EndPoint joinedPeer, CancellationToken token = default)
        => IsDisposed ? new(DisposedTask) : EnqueueAsync(Command.Join(joinedPeer), token);

    private async Task ProcessJoinAsync(EndPoint joinedPeer)
    {
        using var tasks = new PooledArrayBufferWriter<Task>(activeViewCapacity);
        await AddPeerToActiveViewAsync(joinedPeer, true).ConfigureAwait(false);

        // forwards JOIN request to all neighbors excluding joined peer
        foreach (var neighbor in activeView.Remove(joinedPeer))
        {
            tasks.Add(Task.Run(() => ForwardJoinAsync(joinedPeer, neighbor, activeRandomWalkLength, LifecycleToken), LifecycleToken));
        }

        // await responses
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
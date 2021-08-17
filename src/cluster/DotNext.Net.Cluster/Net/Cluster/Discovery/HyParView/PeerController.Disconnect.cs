using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Discovery.HyParView
{
    using static Collections.Generic.Collection;

    public partial class PeerController
    {
        /// <summary>
        /// Sends Disconnect notification to the peer.
        /// </summary>
        /// <param name="peer">The receiver of the notification.</param>
        /// <param name="isAlive">
        /// <see langword="true"/> if sender remains alive;
        /// <see langword="false"/> if sender is shutting down gracefully.
        /// </param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        protected abstract Task DisconnectAsync(EndPoint peer, bool isAlive, CancellationToken token = default);

        /// <summary>
        /// Must be called by transport layer when Disconnect request is received.
        /// </summary>
        /// <param name="sender">The sender of the request which is about to disconnect.</param>
        /// <param name="isAlive">
        /// <see langword="true"/> if the sender remains alive after disconnect;
        /// <see langword="false"/> if the sender is disconnected gracefully and will no longer available in the cluster.
        /// </param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ObjectDisposedException">The controller has been disposed.</exception>
        protected ValueTask EnqueueDisconnectAsync(EndPoint sender, bool isAlive, CancellationToken token = default)
            => IsDisposed ? new(DisposedTask) : EnqueueAsync(Command.Disconnect(sender, isAlive), token);

        private async Task ProcessDisconnectAsync(EndPoint sender, bool isAlive)
        {
            // remove disconnected peer from active view
            var activeViewCopy = activeView.Remove(sender);
            if (ReferenceEquals(activeViewCopy, activeView))
                goto exit;

            activeView = activeViewCopy;
            await DisconnectAsync(sender).ConfigureAwait(false);
            OnPeerGone(sender);

            try
            {
                // move random peer from passive view to active view
                if (passiveView.PeekRandom(random).TryGet(out var activePeer))
                    await AddPeerToActiveViewAsync(activePeer, activeView.IsEmpty).ConfigureAwait(false);
            }
            finally
            {
                if (isAlive)
                    passiveView = passiveView.Add(sender);
                else
                    await DestroyAsync(sender).ConfigureAwait(false);
            }

        exit:
            return;
        }

        /// <summary>
        /// Called automatically when the connection to the remote peer can be closed.
        /// </summary>
        /// <remarks>
        /// Calling of this method indicates that the peer is no longer available.
        /// </remarks>
        /// <param name="peer">The peer to disconnect.</param>
        /// <returns>The task representing asynchronous result of the operation.</returns>
        protected virtual ValueTask DisconnectAsync(EndPoint peer) => new();
    }
}
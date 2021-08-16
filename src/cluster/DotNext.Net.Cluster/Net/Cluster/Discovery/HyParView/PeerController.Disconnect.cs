using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Discovery.HyParView
{
    using static Collections.Generic.Collection;
    using static Threading.LinkedTokenSourceFactory;

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

        private async ValueTask DisconnectCoreAsync(EndPoint senderPeer, bool isAlive, CancellationToken token)
        {
            Debug.Assert(accessLock.IsWriteLockHeld);

            // remove disconnected peer from active view
            var activeViewCopy = activeView.Remove(senderPeer);
            if (ReferenceEquals(activeViewCopy, activeView))
                goto exit;

            activeView = activeViewCopy;
            await DisconnectAsync(senderPeer).ConfigureAwait(false);
            OnPeerGone(senderPeer);

            try
            {
                // move random peer from passive view to active view
                if (passiveView.PeekRandom(random).TryGet(out var activePeer))
                    await AddPeerToActiveViewAsync(activePeer, activeView.IsEmpty, token).ConfigureAwait(false);
            }
            finally
            {
                if (isAlive)
                    passiveView = passiveView.Add(senderPeer);
                else
                    await DestroyAsync(senderPeer).ConfigureAwait(false);
            }

        exit:
            return;
        }

        /// <summary>
        /// Must be called by underlying transport layer when Disconnect notification is received.
        /// </summary>
        /// <param name="sender">The address of the sender peer to disconnect.</param>
        /// <param name="isAlive">
        /// <see langword="true"/> if sender remains alive;
        /// <see langword="false"/> if sender is shutting down gracefully.
        /// </param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        protected async ValueTask OnDisconnectAsync(EndPoint sender, bool isAlive, CancellationToken token)
        {
            var tokenSource = token.LinkTo(LifecycleToken);
            var lockTaken = false;
            try
            {
                await accessLock.EnterWriteLockAsync(token).ConfigureAwait(false);
                lockTaken = true;

                await DisconnectCoreAsync(sender, isAlive, token).ConfigureAwait(false);
            }
            finally
            {
                if (lockTaken)
                    accessLock.ExitWriteLock();

                tokenSource?.Dispose();
            }
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
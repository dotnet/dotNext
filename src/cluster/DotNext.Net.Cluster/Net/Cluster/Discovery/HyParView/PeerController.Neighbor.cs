using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Discovery.HyParView
{
    using static Threading.LinkedTokenSourceFactory;

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
        /// Must be called by underlying transport layer when Neighbor request is received.
        /// </summary>
        /// <param name="neighbor">The neighbor announcement.</param>
        /// <param name="highPriority">
        /// <see langword="true"/> to add the peer to the active view of receiver even if view
        /// is full; otherwise, <see langword="false"/>.
        /// </param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <seealso cref="NeighborAsync(EndPoint, bool, CancellationToken)"></seealso>
        protected async ValueTask OnNeighborAsync(EndPoint neighbor, bool highPriority, CancellationToken token)
        {
            var tokenSource = token.LinkTo(LifecycleToken);
            var lockTaken = false;
            try
            {
                await accessLock.EnterWriteLockAsync(token).ConfigureAwait(false);
                lockTaken = true;

                if (highPriority || activeView.Count < activeViewCapacity)
                    await AddPeerToActiveViewAsync(neighbor, highPriority, token).ConfigureAwait(false);
            }
            finally
            {
                if (lockTaken)
                    accessLock.ExitWriteLock();

                tokenSource?.Dispose();
            }
        }
    }
}
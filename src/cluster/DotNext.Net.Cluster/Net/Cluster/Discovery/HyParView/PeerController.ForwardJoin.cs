using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Discovery.HyParView
{
    using static Collections.Generic.Collection;
    using static Threading.LinkedTokenSourceFactory;

    public partial class PeerController
    {
        /// <summary>
        /// Sends ForwardJoin request to the peer.
        /// </summary>
        /// <param name="receiver">The receiver of the message.</param>
        /// <param name="joinedPeer">The joined peer.</param>
        /// <param name="timeToLive">TTL value that controlls broadcast of ForwardJoin request.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        protected abstract Task ForwardJoinAsync(EndPoint receiver, EndPoint joinedPeer, int timeToLive, CancellationToken token = default);

        /// <summary>
        /// Must be called by underlying transport layer when ForwardJoin request is received.
        /// </summary>
        /// <typeparam name="TAnnouncement">The type representing peer announcement.</typeparam>
        /// <param name="sender">The address of the sender peer.</param>
        /// <param name="joinedPeer">The announcement of the joined peer.</param>
        /// <param name="timeToLive">The maximum number of request redirections.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <seealso cref="ForwardJoinAsync(EndPoint, EndPoint, int, CancellationToken)"/>
        protected async Task OnForwardJoinAsync<TAnnouncement>(EndPoint sender, EndPoint joinedPeer, int timeToLive, CancellationToken token)
        {
            var tokenSource = token.LinkTo(LifecycleToken);
            var lockTaken = false;
            try
            {
                await accessLock.EnterWriteLockAsync(token).ConfigureAwait(false);
                lockTaken = true;

                if (timeToLive == 0 || activeView.IsEmpty)
                {
                    await AddPeerToActiveViewAsync(joinedPeer, true, token).ConfigureAwait(false);
                }
                else
                {
                    if (timeToLive == PassiveRandomWalkLength)
                        await AddPeerToPassiveViewAsync(joinedPeer).ConfigureAwait(false);

                    await (activeView.Remove(sender).PeekRandom(random).TryGet(out var randomActivePeer)
                        ? new ValueTask(ForwardJoinAsync(randomActivePeer, joinedPeer, timeToLive - 1, token))
                        : AddPeerToActiveViewAsync(joinedPeer, true, token)).ConfigureAwait(false);
                }
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
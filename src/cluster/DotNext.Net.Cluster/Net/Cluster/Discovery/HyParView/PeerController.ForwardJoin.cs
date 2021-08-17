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
        /// Must be called by transport layer when ForwardJoin request is received.
        /// </summary>
        /// <param name="sender">The sender of the request.</param>
        /// <param name="joinedPeer">The joined peer.</param>
        /// <param name="timeToLive">The number of hops the request is forwarded.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ObjectDisposedException">The controller has been disposed.</exception>
        protected ValueTask EnqueueForwardJoinAsync(EndPoint sender, EndPoint joinedPeer, int timeToLive, CancellationToken token = default)
            => IsDisposed ? new(DisposedTask) : EnqueueAsync(Command.ForwardJoin(sender, joinedPeer, timeToLive), token);

        private async Task ProcessForwardJoinAsync(EndPoint sender, EndPoint joinedPeer, int timeToLive)
        {
            if (timeToLive == 0 || activeView.IsEmpty)
            {
                await AddPeerToActiveViewAsync(joinedPeer, true).ConfigureAwait(false);
            }
            else
            {
                if (timeToLive == passiveRandomWalkLength)
                    await AddPeerToPassiveViewAsync(joinedPeer).ConfigureAwait(false);

                await (activeView.Remove(sender).PeekRandom(random).TryGet(out var randomActivePeer)
                    ? ForwardJoinAsync(randomActivePeer, joinedPeer, timeToLive - 1, LifecycleToken)
                    : AddPeerToActiveViewAsync(joinedPeer, true)).ConfigureAwait(false);
            }
        }
    }
}
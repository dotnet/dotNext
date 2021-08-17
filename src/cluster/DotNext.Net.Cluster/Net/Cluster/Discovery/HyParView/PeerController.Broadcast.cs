using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Discovery.HyParView
{
    using Buffers;

    public partial class PeerController
    {
        /// <summary>
        /// Spreads the rumour across neighbors.
        /// </summary>
        /// <param name="sender">The rumour sender.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ObjectDisposedException">The controller has been disposed.</exception>
        public ValueTask EnqueueBroadcastAsync(IRumourSender sender, CancellationToken token = default)
            => IsDisposed ? new(DisposedTask) : EnqueueAsync(Command.Broadcast(sender), token);

        private async Task ProcessBroadcastAsync(IRumourSender sender)
        {
            using var failedPeers = new PooledArrayBufferWriter<EndPoint>(activeViewCapacity);
            try
            {
                int activeViewCount;

                do
                {
                    failedPeers.Clear(true);

                    foreach (var peer in activeView)
                    {
                        try
                        {
                            await sender.SendAsync(peer, LifecycleToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException e) when (e.CancellationToken == LifecycleToken)
                        {
                            // the peer controller has stopped, leave the loop without further actions
                            return;
                        }
                        catch (Exception e)
                        {
                            OnError(peer, e);

                            // remember failed peer and try to select another one later
                            failedPeers.Add(peer);
                        }
                    }

                    activeViewCount = activeView.Count;

                    // handle failures
                    foreach (var failedPeer in failedPeers)
                    {
                        // replace failed peer with another one
                        await ProcessDisconnectAsync(failedPeer, false).ConfigureAwait(false);
                    }

                    // all peers from active view has failed, send messages again
                }
                while (activeViewCount > 0 && failedPeers.WrittenCount == activeViewCount);
            }
            finally
            {
                failedPeers.Dispose();
                await sender.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Reports communication error associated with the peer.
        /// </summary>
        /// <param name="peer">The unavailable peer.</param>
        /// <param name="e">The exception describing communication issue.</param>
        protected virtual void OnError(EndPoint peer, Exception e) => Logger.PeerCommunicationFailed(peer, e);
    }
}
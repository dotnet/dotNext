using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Discovery.HyParView
{
    using Buffers;
    using static Threading.LinkedTokenSourceFactory;

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
        /// Must be called by underlying transport layer when Join request is received.
        /// </summary>
        /// <param name="joinedPeer">The address of the joined peer.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <seealso cref="JoinAsync(EndPoint, CancellationToken)"/>
        protected async Task OnJoinAsync(EndPoint joinedPeer, CancellationToken token)
        {
            PooledArrayBufferWriter<Task>? tasks = null;
            var tokenSource = token.LinkTo(LifecycleToken);
            var lockTaken = false;
            try
            {
                await accessLock.EnterWriteLockAsync(token).ConfigureAwait(false);
                lockTaken = true;

                await AddPeerToActiveViewAsync(joinedPeer, true, token).ConfigureAwait(false);

                // forwards JOIN request to all neighbors including joined peer
                tasks = new(activeViewCapacity);
                foreach (var neighbor in activeView.Remove(joinedPeer))
                {
                    if (!joinedPeer.Equals(neighbor))
                    {
                        tasks.Add(Task.Run(() => ForwardJoinAsync(joinedPeer, neighbor, ActiveRandomWalkLength, token), token));
                    }
                }

                // await responses
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            finally
            {
                if (lockTaken)
                    accessLock.ExitWriteLock();

                tasks?.Dispose();
                tokenSource?.Dispose();
            }
        }
    }
}
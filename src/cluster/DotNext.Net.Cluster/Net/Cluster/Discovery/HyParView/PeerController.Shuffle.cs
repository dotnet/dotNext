using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Discovery.HyParView
{
    using Buffers;
    using Collections.Generic;

    public partial class PeerController
    {
        private Task shuffleTask;

        // implements Shuffle message passing loop
        private async Task ShuffleLoopAsync(TimeSpan period)
        {
            while (!queue.Reader.Completion.IsCompleted)
            {
                try
                {
                    await Task.Delay(period, LifecycleToken).ConfigureAwait(false);
                    await queue.Writer.WriteAsync(Command.ForceShuffle(), LifecycleToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException e) when (e.CancellationToken == LifecycleToken)
                {
                    break;
                }
            }
        }

        private async Task ProcessShuffleAsync()
        {
            if (activeView.PeekRandom(random).TryGet(out var activePeer))
            {
                PooledArrayBufferWriter<EndPoint> peersToSend;

                using (var activeViewCopy = activeView.Remove(activePeer).Copy())
                {
                    activeViewCopy.Memory.Span.Shuffle(random);

                    using var passiveViewCopy = passiveView.Copy();
                    passiveViewCopy.Memory.Span.Shuffle(random);

                    // add randomly selected peers from active and passive views
                    peersToSend = new PooledArrayBufferWriter<EndPoint>(shuffleActiveViewCount + shufflePassiveViewCount);
                    peersToSend.Write(activeViewCopy.Memory.Span.TrimLength(shuffleActiveViewCount));
                    peersToSend.Write(passiveViewCopy.Memory.Span.TrimLength(shufflePassiveViewCount));
                }

                // attempts to send Shuffle message to the randomly selected peer
                try
                {
                    await ShuffleAsync(activePeer, null, peersToSend, shuffleRandomWalkLength, LifecycleToken).ConfigureAwait(false);
                }
                catch (Exception e) when (e is not OperationCanceledException canceledEx || canceledEx.CancellationToken != LifecycleToken)
                {
                    await ProcessDisconnectAsync(activePeer, false).ConfigureAwait(false);
                }
                finally
                {
                    peersToSend.Dispose();
                }
            }
        }

        /// <summary>
        /// Forces sending of Shuffle message to randomly selected peer from active view.
        /// </summary>
        /// <remarks>
        /// Use this method only if <see cref="IPeerConfiguration.ShufflePeriod"/> is set to <see langword="null"/>.
        /// </remarks>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ObjectDisposedException">The controller has been disposed.</exception>
        /// <exception cref="InvalidOperationException">Manual shuffle is not allowed.</exception>
        public ValueTask EnqueueShuffleAsync(CancellationToken token = default)
        {
            if (IsDisposed)
                return new(DisposedTask);

            if (shufflePeriod is not null)
                return ValueTask.FromException(new InvalidOperationException());

            return EnqueueAsync(Command.ForceShuffle(), token);
        }

        /// <summary>
        /// Sends Shuffle message to the specified peer.
        /// </summary>
        /// <param name="receiver">The receiver of the message.</param>
        /// <param name="origin">
        /// The original sender of the initial Shuffle message;
        /// or <see langword="null"/> if the current peer is the sender of the message.
        /// </param>
        /// <param name="peers">The collection of peers to announce.</param>
        /// <param name="timeToLive">The number of hops to reach the receiver of the message.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        protected abstract Task ShuffleAsync(EndPoint receiver, EndPoint? origin, IReadOnlyCollection<EndPoint> peers, int timeToLive, CancellationToken token = default);

        /// <summary>
        /// Must be called by transport layer when Shuffle request is received.
        /// </summary>
        /// <param name="sender">The announcement of the neighbor peer.</param>
        /// <param name="origin">The initial sender of the request.</param>
        /// <param name="peers">The portion of active and passive view randomly selected by the initial sender.</param>
        /// <param name="timeToLive">The number of random hops the request is forwarded.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ObjectDisposedException">The controller has been disposed.</exception>
        protected ValueTask EnqueueShuffleAsync(EndPoint sender, EndPoint origin, IReadOnlyCollection<EndPoint> peers, int timeToLive, CancellationToken token = default)
            => IsDisposed ? new(DisposedTask) : EnqueueAsync(Command.Shuffle(sender, origin, peers, timeToLive), token);

        private async Task ProcessShuffleAsync(EndPoint sender, EndPoint origin, IReadOnlyCollection<EndPoint> announcement, int ttl)
        {
            if (announcement.Count == 0)
                return;

            // add announced peers to the local passive view
            if (ttl == 0)
            {
                using var randomizedPassiveView = new PooledArrayBufferWriter<EndPoint>();

                // send random part of passive view back to origin
                randomizedPassiveView.AddAll(passiveView);
                randomizedPassiveView.WrittenArray.AsSpan().Shuffle(random);
                if (randomizedPassiveView.WrittenCount > announcement.Count)
                    randomizedPassiveView.RemoveLast(randomizedPassiveView.WrittenCount - announcement.Count);
                await ShuffleReplyAsync(origin, randomizedPassiveView, LifecycleToken).ConfigureAwait(false);

                await AddPeersToPassiveViewAsync(announcement).ConfigureAwait(false);
            }
            else if (activeView.Except(new[] { sender, origin }).PeekRandom(random).TryGet(out var activePeer))
            {
                // resend announcement
                await ShuffleAsync(activePeer, origin, announcement, ttl - 1, LifecycleToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Sends reply to the original sender of Shuffle message.
        /// </summary>
        /// <param name="receiver">The original sender of Shuffle message.</param>
        /// <param name="peers">The portion of peers from local passive view.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        protected abstract Task ShuffleReplyAsync(EndPoint receiver, IReadOnlyCollection<EndPoint> peers, CancellationToken token = default);

        /// <summary>
        /// Must be called by transport layer when Shuffle reply is received.
        /// </summary>
        /// <param name="peers">The portion of passive view randomly selected by the final receiver of Shuffle request.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ObjectDisposedException">The controller has been disposed.</exception>
        protected ValueTask EnqueueShuffleReplyAsync(IReadOnlyCollection<EndPoint> peers, CancellationToken token = default)
            => IsDisposed ? new(DisposedTask) : EnqueueAsync(Command.ShuffleReply(peers), token);

        private Task ProcessShuffleReply(IReadOnlyCollection<EndPoint> announcement)
            => AddPeersToPassiveViewAsync(announcement).AsTask();
    }
}
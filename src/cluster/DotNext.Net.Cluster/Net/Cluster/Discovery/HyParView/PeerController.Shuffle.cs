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
    using static Threading.LinkedTokenSourceFactory;
    using AtomicBoolean = Threading.AtomicBoolean;

    public partial class PeerController
    {
        private AtomicBoolean skipShuffle;
        private Task shuffleTask;
        private int? shuffleActiveViewCount, shufflePassiveViewCount, shuffleRandomWalkLength;

        /// <summary>
        /// Gets the number of peers from active view to be included into Shuffle message.
        /// </summary>
        public int ShuffleActiveViewCount
        {
            get => shuffleActiveViewCount ?? activeViewCapacity - 1;
            set => shuffleActiveViewCount = value > 0 && value <= activeViewCapacity ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }

        /// <summary>
        /// Gets the number of peers from passive view to be included into Shuffle message.
        /// </summary>
        public int ShufflePassiveViewCount
        {
            get => shufflePassiveViewCount ?? passiveViewCapacity - 1;
            set => shufflePassiveViewCount = value > 0 && value <= passiveViewCapacity ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }

        /// <summary>
        /// Gets the maximum number of hops a Shuffle message is propagated.
        /// </summary>
        public int ShuffleRandomWalkLength
        {
            get => shuffleRandomWalkLength ?? PassiveRandomWalkLength;
            set => shuffleRandomWalkLength = value > 0 && value <= ActiveRandomWalkLength ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }

        /// <summary>
        /// Gets or sets shuffle period.
        /// </summary>
        /// <remarks>
        /// If <see langword="null"/> then <see cref="ShuffleAsync(CancellationToken)"/> must be called
        /// manually when needed.
        /// </remarks>
        public TimeSpan? ShufflePeriod { get; set; }

        /// <summary>
        /// Sets randomly generated value to <see cref="ShufflePeriod"/> property.
        /// </summary>
        /// <param name="minValue">The lower bound (inclusive) of the period, in milliseconds.</param>
        /// <param name="maxValue">The upper bound (inclusive) of the period, in milliseconds.</param>
        public void SetRandomShufflePeriod(int minValue, int maxValue)
        {
            if (minValue <= 0)
                throw new ArgumentOutOfRangeException(nameof(minValue));

            if (maxValue <= 0 || maxValue == int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(maxValue));

            ShufflePeriod = TimeSpan.FromMilliseconds(random.Next(minValue, maxValue + 1));
        }

        // implements Shuffle message passing loop
        private async Task ShuffleAsync(TimeSpan period, CancellationToken token)
        {
            do
            {
                await Task.Delay(period, token).ConfigureAwait(false);
                if (skipShuffle.TrueToFalse())
                    continue;

                if (!await ShuffleAsync(false, token).ConfigureAwait(false))
                    break;
            }
            while (!LifecycleToken.IsCancellationRequested);
        }

        private async Task<bool> ShuffleAsync(bool throwOnCanceled, CancellationToken token)
        {
            var failedPeer = Optional<EndPoint>.None;
            using var tokenSource = token.LinkTo(LifecycleToken);
            var lockTaken = false;
            try
            {
                await accessLock.EnterReadLockAsync(token).ConfigureAwait(false);
                lockTaken = true;

                if (activeView.PeekRandom(random).TryGet(out var activePeer))
                {
                    PooledArrayBufferWriter<EndPoint> peersToSend;

                    using (var activeViewCopy = activeView.Remove(activePeer).Copy())
                    {
                        activeViewCopy.Memory.Span.Shuffle(random);

                        using var passiveViewCopy = passiveView.Copy();
                        passiveViewCopy.Memory.Span.Shuffle(random);

                        // add randomly selected peers from active and passive views
                        peersToSend = new PooledArrayBufferWriter<EndPoint>(ShuffleActiveViewCount + ShufflePassiveViewCount);
                        peersToSend.Write(activeViewCopy.Memory.Span.TrimLength(ShuffleActiveViewCount));
                        peersToSend.Write(passiveViewCopy.Memory.Span.TrimLength(ShufflePassiveViewCount));
                    }

                    // attempts to send Shuffle message to the randomly selected peer
                    try
                    {
                        await ShuffleAsync(activePeer, null, peersToSend, ShuffleRandomWalkLength, token).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        // if canceled then just leave the loop
                        if (e is OperationCanceledException canceledEx && canceledEx.CancellationToken == token)
                            return throwOnCanceled ? false : throw canceledEx;

                        // remember failed peer and remove it from active view later
                        failedPeer = activePeer;
                    }
                    finally
                    {
                        peersToSend.Dispose();
                    }
                }
            }
            catch (OperationCanceledException) when (throwOnCanceled)
            {
                return false;
            }
            finally
            {
                if (lockTaken)
                    accessLock.ExitReadLock();
            }

            // remove failed peer inside of separated lock
            if (failedPeer.TryGet(out var peerToRemove))
            {
                lockTaken = false;
                try
                {
                    await accessLock.EnterWriteLockAsync(token).ConfigureAwait(false);
                    lockTaken = true;

                    await DisconnectCoreAsync(peerToRemove, false, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (throwOnCanceled)
                {
                    return false;
                }
                finally
                {
                    if (lockTaken)
                        accessLock.ExitWriteLock();
                }
            }

            return true;
        }

        /// <summary>
        /// Forces sending of Shuffle message to randomly selected peer from active view.
        /// </summary>
        /// <remarks>
        /// Use this method only if <see cref="ShufflePeriod"/> is set to <see langword="null"/>.
        /// </remarks>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task ShuffleAsync(CancellationToken token) => ShuffleAsync(true, token);

        /// <summary>
        /// Sends Shuffle message to the specified peer.
        /// </summary>
        /// <param name="receiver">The receiver of the message.</param>
        /// <param name="origin">
        /// The original sender of the initial Shuffle message;
        /// or <see langword="null"/> if the current peer is the sender of the message.
        /// </param>
        /// <param name="peers">The collection of peers to announce.</param>
        /// <param name="ttl">The number of hops to reach the receiver of the message.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        protected abstract Task ShuffleAsync(EndPoint receiver, EndPoint? origin, IReadOnlyCollection<EndPoint> peers, int ttl, CancellationToken token);

        /// <summary>
        /// Must be called by underlying transport layer when Shuffle request is received.
        /// </summary>
        /// <param name="sender">The immediate sender of the message.</param>
        /// <param name="origin">The original sender of the message.</param>
        /// <param name="announcement">A portion active and passive views from the original sender.</param>
        /// <param name="ttl">The number of hops to reach the receiver of the message.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <seealso cref="ShuffleAsync(EndPoint, EndPoint?, IReadOnlyCollection{EndPoint}, int, CancellationToken)"/>
        protected async Task OnShuffleAsync(EndPoint sender, EndPoint origin, IReadOnlyCollection<EndPoint> announcement, int ttl, CancellationToken token)
        {
            if (announcement.Count == 0)
                return;

            // skip background shuffle operation
            skipShuffle.Value = true;

            using var tokenSource = token.LinkTo(LifecycleToken);

            // add announced peers to the local passive view
            if (ttl == 0)
            {
                await accessLock.EnterWriteLockAsync(token).ConfigureAwait(false);
                PooledArrayBufferWriter<EndPoint>? randomizedPassiveView = null;
                try
                {
                    // send random part of passive view back to origin
                    randomizedPassiveView = new();
                    randomizedPassiveView.AddAll(passiveView);
                    randomizedPassiveView.WrittenArray.AsSpan().Shuffle(random);
                    if (randomizedPassiveView.WrittenCount > announcement.Count)
                        randomizedPassiveView.RemoveLast(randomizedPassiveView.WrittenCount - announcement.Count);
                    await AddPeersToPassiveViewAsync(origin, randomizedPassiveView, token).ConfigureAwait(false);

                    await AddPeersToPassiveViewAsync(announcement).ConfigureAwait(false);
                }
                finally
                {
                    accessLock.ExitWriteLock();
                    randomizedPassiveView?.Dispose();
                }
            }
            else
            {
                // resend announcement
                await accessLock.EnterReadLockAsync(token).ConfigureAwait(false);
                try
                {
                    if (activeView.Except(new[] { sender, origin }).PeekRandom(random).TryGet(out var activePeer))
                        await ShuffleAsync(activePeer, origin, announcement, ttl - 1, token).ConfigureAwait(false);
                }
                finally
                {
                    accessLock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Sends ShuffleReply to the original sender of Shuffle message.
        /// </summary>
        /// <param name="receiver">The original sender of Shuffle message.</param>
        /// <param name="peers">The portion of peers from local passive view.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        protected abstract Task AddPeersToPassiveViewAsync(EndPoint receiver, IReadOnlyCollection<EndPoint> peers, CancellationToken token);

        /// <summary>
        /// Must be called by underlying transport layer when Shuffle request is received.
        /// </summary>
        /// <param name="announcement">The portion of passive view of the peer that has received a Shuffle message with TTL = 0.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        protected async Task OnAddPeersToPassiveViewAsync(IReadOnlyCollection<EndPoint> announcement, CancellationToken token)
        {
            var tokenSource = token.LinkTo(LifecycleToken);
            var lockTaken = false;
            try
            {
                await accessLock.EnterWriteLockAsync(token).ConfigureAwait(false);
                lockTaken = true;

                await AddPeersToPassiveViewAsync(announcement).ConfigureAwait(false);
            }
            finally
            {
                if (lockTaken)
                    accessLock.ExitReadLock();

                tokenSource?.Dispose();
            }
        }
    }
}
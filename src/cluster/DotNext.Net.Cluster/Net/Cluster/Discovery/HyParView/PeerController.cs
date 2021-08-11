using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Discovery.HyParView
{
    using IO;
    using static Collections.Generic.Collection;
    using ExceptionAggregator = Runtime.ExceptionServices.ExceptionAggregator;

    /// <summary>
    /// Represents local peer supporting HyParView membership
    /// protocol for Gossip-based broadcast.
    /// </summary>
    /// <typeparam name="TPeerContact">Represents information about remote peer.</typeparam>
    /// <seealso href="https://asc.di.fct.unl.pt/~jleitao/pdf/dsn07-leitao.pdf">HyParView: a membership protocol for reliable gossip-based broadcast</seealso>
    public abstract class PeerController<TPeerContact>
        where TPeerContact : IPeer
    {
        private readonly int activeViewCapacity, passiveViewCapacity;
        private readonly Random random;
        private ImmutableHashSet<TPeerContact> activeView, passiveView;
        private EventHandler<TPeerContact>? peerDiscoveredHandlers, peerGoneHandlers;

        /// <summary>
        /// Initializes a new HyParView protocol controller.
        /// </summary>
        /// <param name="activeViewCapacity">The capacity of active view representing resolved peers.</param>
        /// <param name="passiveViewCapacity">The capacity of backlog for peers.</param>
        /// <param name="activeRandomWalkLength">The number of hops a ForwardJoin request is propagated.</param>
        /// <param name="passiveRandomWalkLength">The number that specifies at which point in the walk the peer is inserted into passive view.</param>
        /// <param name="peerComparer">The comparer used to check identity of <typeparamref name="TPeerContact"/> objects.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="activeViewCapacity"/> is less than or equal to 1;
        /// or <paramref name="passiveViewCapacity"/> is less than <paramref name="activeViewCapacity"/>;
        /// or <paramref name="activeRandomWalkLength"/> is less than or equal to zero;
        /// or <paramref name="passiveRandomWalkLength"/> is greater than <paramref name="activeRandomWalkLength"/>;
        /// or <paramref name="passiveRandomWalkLength"/> is less than or equal to zero.
        /// </exception>
        protected PeerController(int activeViewCapacity, int passiveViewCapacity, int activeRandomWalkLength, int passiveRandomWalkLength, IEqualityComparer<TPeerContact>? peerComparer)
        {
            if (activeViewCapacity <= 1)
                throw new ArgumentOutOfRangeException(nameof(activeViewCapacity));
            if (passiveViewCapacity < activeViewCapacity)
                throw new ArgumentOutOfRangeException(nameof(passiveViewCapacity));
            if (activeRandomWalkLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(activeRandomWalkLength));
            if (passiveRandomWalkLength > activeRandomWalkLength || passiveRandomWalkLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(passiveRandomWalkLength));

            this.activeViewCapacity = activeViewCapacity;
            this.passiveViewCapacity = passiveViewCapacity;
            random = new();
            ActiveRandomWalkLength = activeRandomWalkLength;
            PassiveRandomWalkLength = passiveRandomWalkLength;
            activeView = ImmutableHashSet.Create(peerComparer);
            passiveView = ImmutableHashSet.Create(peerComparer);
        }

        /// <summary>
        /// Gets a collection of discovered peers.
        /// </summary>
        public IReadOnlyCollection<TPeerContact> Neighbors => activeView;

        /// <summary>
        /// Gets the maximum number of hops a ForwardJoin request is propagated.
        /// </summary>
        public int ActiveRandomWalkLength { get; }

        /// <summary>
        /// Gets the value specifies at which point in the walk the peer is inserted into passive view.
        /// </summary>
        public int PassiveRandomWalkLength { get; }

        /// <summary>
        /// Sends Join request to contact node.
        /// </summary>
        /// <param name="contactNode">The address of the contact node.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing communication operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public abstract Task JoinAsync(EndPoint contactNode, CancellationToken token);

        /// <summary>
        /// Must be called by underlying transport layer when Join request is received.
        /// </summary>
        /// <typeparam name="TAnnouncement">The transport-specified carrier of announcement data.</typeparam>
        /// <param name="announcement">The announcement data.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of the operation.</returns>
        /// <seealso cref="JoinAsync(EndPoint, CancellationToken)"/>
        protected async ValueTask OnJoinAsync<TAnnouncement>(TAnnouncement announcement, CancellationToken token)
            where TAnnouncement : notnull, IDataTransferObject
        {
            var joinedPeer = await ParseAnnouncementAsync(announcement, token).ConfigureAwait(false);
            await AddPeerToActiveViewAsync(joinedPeer, true, token).ConfigureAwait(false);

            // forwards JOIN request to all neighbors
            var tasks = new List<Task>(activeViewCapacity);
            foreach (var neighbor in activeView)
            {
                if (!joinedPeer.Equals(neighbor))
                {
                    tasks.Add(Task.Run(() => ForwardJoinAsync(joinedPeer, neighbor, ActiveRandomWalkLength, token), token));
                }
            }

            // await responses
            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            finally
            {
                tasks.Clear();
            }
        }

        /// <summary>
        /// Sends ForwardJoin request to the peer.
        /// </summary>
        /// <param name="joinedPeer">The joined peer.</param>
        /// <param name="neighbor">The neighbor peer.</param>
        /// <param name="timeToLive">TTL value that controlls broadcast of ForwardJoin request.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing </returns>
        protected abstract Task ForwardJoinAsync(TPeerContact joinedPeer, TPeerContact neighbor, int timeToLive, CancellationToken token = default);

        protected async ValueTask OnForwardJoinAsync<TAnnouncement>(EndPoint sender, TAnnouncement announcement, int timeToLive, CancellationToken token)
            where TAnnouncement : notnull, IDataTransferObject
        {
            var joinedPeer = await ParseAnnouncementAsync(announcement, token).ConfigureAwait(false);

            if (timeToLive == 0 || activeView.IsEmpty)
            {
                await AddPeerToActiveViewAsync(joinedPeer, true, token).ConfigureAwait(false);
            }
            else
            {
                if (timeToLive == PassiveRandomWalkLength)
                    await AddPeerToPassiveViewAsync(joinedPeer).ConfigureAwait(false);

                await (Except(activeView, sender).PeekRandom(random).TryGet(out var randomActivePeer)
                    ? new ValueTask(ForwardJoinAsync(joinedPeer, randomActivePeer, timeToLive - 1, token))
                    : AddPeerToActiveViewAsync(joinedPeer, true, token)).ConfigureAwait(false);
            }

            static IReadOnlyCollection<TPeerContact> Except(ImmutableHashSet<TPeerContact> peers, EndPoint address)
                => FindByAddress(peers, address).TryGet(out var peerToRemove) ? peers.Remove(peerToRemove) : peers;
        }

        protected abstract Task NeighborAsync(TPeerContact neighbor, bool highPriority, CancellationToken token);

        protected async ValueTask OnNeighborAsync<TAnnouncement>(TAnnouncement neighborInfo, bool highPriority, CancellationToken token)
            where TAnnouncement : notnull, IDataTransferObject
        {
            var neighbor = await ParseAnnouncementAsync(neighborInfo, token).ConfigureAwait(false);
            await (highPriority || activeView.Count < activeViewCapacity
                ? AddPeerToActiveViewAsync(neighbor, highPriority, token)
                : DestroyAsync(neighbor)).ConfigureAwait(false);
        }

        protected abstract Task DisconnectAsync(TPeerContact peer, bool isAlive, CancellationToken token = default);

        private async ValueTask OnDisconnectAsync(TPeerContact senderPeer, bool isAlive, CancellationToken token)
        {
            // remove disconnected peer from active view
            activeView = activeView.Remove(senderPeer);
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
            }
        }

        protected ValueTask OnDisconnectAsync(EndPoint sender, bool isAlive, CancellationToken token)
            => FindByAddress(activeView, sender).TryGet(out var senderPeer) ? OnDisconnectAsync(senderPeer, isAlive, token) : new();

        /// <summary>
        /// Creates local representation of the remote peer.
        /// </summary>
        /// <typeparam name="TAnnouncement">The transport-specified carrier of announcement data.</typeparam>
        /// <param name="announcement">The announcement information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The local representation of the remote peer.</returns>
        protected abstract ValueTask<TPeerContact> ParseAnnouncementAsync<TAnnouncement>(TAnnouncement announcement, CancellationToken token)
            where TAnnouncement : notnull, IDataTransferObject;

        /// <summary>
        /// Called automatically when the connection to the remote peer can be closed.
        /// </summary>
        /// <remarks>
        /// Calling of this method indicates that the peer is no longer available.
        /// </remarks>
        /// <param name="peer">The peer to disconnect.</param>
        /// <returns>The task representing asynchronous result of the operation.</returns>
        protected virtual ValueTask DisconnectAsync(TPeerContact peer) => new();

        public event EventHandler<TPeerContact> PeerGone
        {
            add => peerGoneHandlers += value;
            remove => peerGoneHandlers -= value;
        }

        protected virtual void OnPeerGone(TPeerContact peer)
            => peerGoneHandlers?.Invoke(this, peer);

        /// <summary>
        /// Releases all local resources associated with the remote peer.
        /// </summary>
        /// <remarks>
        /// By default, this method does nothing.
        /// </remarks>
        /// <param name="peer">The peer to dispose.</param>
        /// <returns>The task representing asynchronous result of the operation.</returns>
        protected virtual ValueTask DestroyAsync(TPeerContact peer) => new();

        public async Task BroadcastAsync<TMessage>(Func<TMessage, TPeerContact, CancellationToken, Task> messageSender, TMessage message, CancellationToken token = default)
        {
            ICollection<(TPeerContact, Task)> responses = new LinkedList<(TPeerContact, Task)>();

            // send message in parallel
            foreach (var peer in activeView)
            {
                var task = Task.Run(() => messageSender.Invoke(message, peer, token), token);
                responses.Add((peer, task));
            }

            // synchronize responses
            var exceptions = new ExceptionAggregator();
            var peersToRemove = new HashSet<TPeerContact>(responses.Count, activeView.KeyComparer);
            foreach (var (peer, response) in responses)
            {
                try
                {
                    await response.ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    // if operation canceled, keep peer in active view
                    exceptions.Add(e);
                    if (e is not OperationCanceledException canceledEx || canceledEx.CancellationToken != token)
                        peersToRemove.Add(peer);
                }
            }

            responses.Clear(); // help GC

            // remove failed peers from active view
            foreach (var peer in peersToRemove)
            {
                try
                {
                    await OnDisconnectAsync(peer, false, token).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }

            // cleanup and throw
            peersToRemove.Clear();
            exceptions.ThrowIfNeeded();
        }

        private ValueTask AddPeerToPassiveViewAsync(TPeerContact peer)
        {
            var result = Optional.None<TPeerContact>();
            if (activeView.Contains(peer) || passiveView.Contains(peer))
                goto exit;

            if (passiveView.Count >= passiveViewCapacity && passiveView.PeekRandom(random).TryGet(out var removedPeer))
            {
                passiveView = passiveView.Remove(removedPeer);
                result = removedPeer;
            }

            passiveView = passiveView.Add(peer);
        exit:
            return result.TryGet(out var peerToDestroy) ? DestroyAsync(peerToDestroy) : new();
        }

        /// <summary>
        /// Represents an event raised when a new remote peer has been discovered.
        /// </summary>
        public event EventHandler<TPeerContact> PeerDiscovered
        {
            add => peerDiscoveredHandlers += value;
            remove => peerDiscoveredHandlers -= value;
        }

        /// <summary>
        /// Called automatically when a new remote peer has been discovered.
        /// </summary>
        /// <param name="discoveredPeer">The discovered remote peer.</param>
        protected virtual void OnPeerDiscovered(TPeerContact discoveredPeer)
            => peerDiscoveredHandlers?.Invoke(this, discoveredPeer);

        private async ValueTask AddPeerToActiveViewAsync(TPeerContact peer, bool highPriority, CancellationToken token = default)
        {
            if (activeView.Contains(peer))
                return;

            // allocate space in active view if it is full
            if (activeView.Count >= activeViewCapacity && activeView.PeekRandom(random).TryGet(out var removedPeer))
            {
                activeView = activeView.Remove(removedPeer);
                await DisconnectAsync(removedPeer, true, token).ConfigureAwait(false);
                await DisconnectAsync(removedPeer).ConfigureAwait(false);
                OnPeerGone(removedPeer);
                await AddPeerToPassiveViewAsync(removedPeer).ConfigureAwait(false);
            }

            passiveView = passiveView.Remove(peer);
            activeView = activeView.Add(peer);
            await NeighborAsync(peer, highPriority, token).ConfigureAwait(false);
            OnPeerDiscovered(peer);
        }

        private static Optional<TPeerContact> FindByAddress(IEnumerable<TPeerContact> peers, EndPoint address)
        {
            foreach (var candidate in peers)
            {
                if (Equals(address, candidate.EndPoint))
                    return candidate;
            }

            return Optional.None<TPeerContact>();
        }
    }
}
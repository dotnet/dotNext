using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Discovery.HyParView
{
    using Buffers;
    using Collections.Generic;
    using Threading;
    using ExceptionAggregator = Runtime.ExceptionServices.ExceptionAggregator;

    /// <summary>
    /// Represents local peer supporting HyParView membership
    /// protocol for Gossip-based broadcast.
    /// </summary>
    /// <remarks>
    /// This controller implements core logic of HyParView algorithm without transport-specific details.
    /// </remarks>
    /// <seealso href="https://asc.di.fct.unl.pt/~jleitao/pdf/dsn07-leitao.pdf">HyParView: a membership protocol for reliable gossip-based broadcast</seealso>
    public abstract partial class PeerController : Disposable, IAsyncDisposable
    {
        private readonly int activeViewCapacity, passiveViewCapacity;
        private readonly Random random;
        private readonly CancellationTokenSource lifecycleTokenSource;
        private readonly AsyncReaderWriterLock accessLock;
        private ImmutableHashSet<EndPoint> activeView, passiveView;
        private EventHandler<EndPoint>? peerDiscoveredHandlers, peerGoneHandlers;

        /// <summary>
        /// Initializes a new HyParView protocol controller.
        /// </summary>
        /// <param name="activeViewCapacity">The capacity of active view representing resolved peers.</param>
        /// <param name="passiveViewCapacity">The capacity of backlog for peers.</param>
        /// <param name="activeRandomWalkLength">The number of hops a ForwardJoin request is propagated.</param>
        /// <param name="passiveRandomWalkLength">The number that specifies at which point in the walk the peer is inserted into passive view.</param>
        /// <param name="peerComparer">The comparer used to check identity of peer address.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="activeViewCapacity"/> is less than or equal to 1;
        /// or <paramref name="passiveViewCapacity"/> is less than <paramref name="activeViewCapacity"/>;
        /// or <paramref name="activeRandomWalkLength"/> is less than or equal to zero;
        /// or <paramref name="passiveRandomWalkLength"/> is greater than <paramref name="activeRandomWalkLength"/>;
        /// or <paramref name="passiveRandomWalkLength"/> is less than or equal to zero.
        /// </exception>
        protected PeerController(int activeViewCapacity, int passiveViewCapacity, int activeRandomWalkLength, int passiveRandomWalkLength, IEqualityComparer<EndPoint>? peerComparer)
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
            accessLock = new();
            lifecycleTokenSource = new();
            LifecycleToken = lifecycleTokenSource.Token;
            shuffleTask = Task.CompletedTask;
        }

        /// <summary>
        /// Gets the token associated with the lifecycle of this object.
        /// </summary>
        protected CancellationToken LifecycleToken { get; }

        /// <summary>
        /// Gets a collection of discovered peers.
        /// </summary>
        public IReadOnlyCollection<EndPoint> Neighbors => activeView; // TODO: Use IReadOnlySet in .NET 6

        /// <summary>
        /// Gets the maximum number of hops a ForwardJoin request is propagated.
        /// </summary>
        public int ActiveRandomWalkLength { get; }

        /// <summary>
        /// Gets the value specifies at which point in the walk the peer is inserted into passive view.
        /// </summary>
        public int PassiveRandomWalkLength { get; }

        /// <summary>
        /// Starts serving HyParView messages and join to the cluster.
        /// </summary>
        /// <param name="contactNode">The contact node used to announce the current peer.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of </returns>
        public async Task StartAsync(EndPoint contactNode, CancellationToken token)
        {
            await JoinAsync(contactNode, token).ConfigureAwait(false);

            if (ShufflePeriod.TryGetValue(out var period))
                shuffleTask = ShuffleAsync(period, LifecycleToken);
        }

        /// <summary>
        /// An event raised when the visible neighbor becomes unavailable.
        /// </summary>
        public event EventHandler<EndPoint> PeerGone
        {
            add => peerGoneHandlers += value;
            remove => peerGoneHandlers -= value;
        }

        private void OnPeerGone(EndPoint peer)
        {
            var handlers = peerGoneHandlers;
            if (handlers is not null)
                ThreadPool.QueueUserWorkItem<(EventHandler<EndPoint> Handler, object Sender, EndPoint Peer)>(static args => args.Handler(args.Sender, args.Peer), (handlers, this, peer), false);
        }

        /// <summary>
        /// Releases all local resources associated with the remote peer.
        /// </summary>
        /// <remarks>
        /// By default, this method does nothing.
        /// </remarks>
        /// <param name="peer">The peer to dispose.</param>
        /// <returns>The task representing asynchronous result of the operation.</returns>
        protected virtual ValueTask DestroyAsync(EndPoint peer) => new();

        /// <summary>
        /// Releases all local resources associated with the remote peer.
        /// </summary>
        /// <remarks>
        /// By default, this method does nothing.
        /// </remarks>
        /// <param name="peer">The peer to destroy.</param>
        protected virtual void Destroy(EndPoint peer)
        {
        }

        /// <summary>
        /// Spreads rumour to the neighbors.
        /// </summary>
        /// <remarks>
        /// If operation canceled then the peer will not be removed from active view.
        /// </remarks>
        /// <typeparam name="TRumour">The type of the rumour.</typeparam>
        /// <param name="rumourSender">The delegate implementing transport-specific logic for transmitting rumour over the wire.</param>
        /// <param name="rumour">The rumour to spread.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result of the operation.</returns>
        /// <exception cref="AggregateException">Two or more peers are unavailable.</exception>
        public async Task BroadcastAsync<TRumour>(Func<TRumour, EndPoint, CancellationToken, Task> rumourSender, TRumour rumour, CancellationToken token = default)
        {
            ICollection<(EndPoint, Task)> responses = new LinkedList<(EndPoint, Task)>();
            var peersToRemove = new HashSet<EndPoint>(activeView.KeyComparer);
            var tryAgain = false;
            ExceptionAggregator exceptions;

            do
            {
                // send message in parallel, use read lock
                await accessLock.EnterReadLockAsync(token).ConfigureAwait(false);
                try
                {
                    foreach (var peer in activeView)
                    {
                        var task = Task.Run(() => rumourSender(rumour, peer, token), token);
                        responses.Add((peer, task));
                    }

                    exceptions = new ExceptionAggregator();
                    peersToRemove.EnsureCapacity(responses.Count);

                    // synchronize responses
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

                    // Handle situation when all peers from active view are unavailable.
                    // If so, then refill active view with peers from passive view.
                    tryAgain = activeView.Count > 0 && peersToRemove.Count == activeView.Count;
                }
                finally
                {
                    accessLock.ExitReadLock();
                    responses.Clear();
                }

                // remove failed peers from active view
                await accessLock.EnterWriteLockAsync(token).ConfigureAwait(false);
                try
                {
                    foreach (var peer in peersToRemove)
                    {
                        try
                        {
                            await DisconnectCoreAsync(peer, false, token).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            exceptions.Add(e);
                        }
                    }
                }
                finally
                {
                    accessLock.ExitWriteLock();
                    peersToRemove.Clear();
                }
            }
            while (tryAgain);

            exceptions.ThrowIfNeeded();
        }

        private ValueTask AddPeerToPassiveViewAsync(EndPoint peer)
        {
            Debug.Assert(accessLock.IsWriteLockHeld);

            var result = Optional.None<EndPoint>();
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

        private async ValueTask AddPeersToPassiveViewAsync(IEnumerable<EndPoint> peers)
        {
            Debug.Assert(accessLock.IsWriteLockHeld);

            var passiveViewCopy = passiveView.ToBuilder();

            foreach (var peer in peers)
            {
                if (activeView.Contains(peer) || passiveViewCopy.Contains(peer))
                    continue;

                if (passiveViewCopy.Count >= passiveViewCapacity && passiveViewCopy.PeekRandom(random).TryGet(out var removedPeer))
                {
                    passiveViewCopy.Remove(removedPeer);
                    await DestroyAsync(removedPeer).ConfigureAwait(false);
                }

                passiveViewCopy.Add(peer);
            }

            passiveView = passiveViewCopy.ToImmutable();
        }

        /// <summary>
        /// An event raised when a new remote peer has been discovered.
        /// </summary>
        public event EventHandler<EndPoint> PeerDiscovered
        {
            add => peerDiscoveredHandlers += value;
            remove => peerDiscoveredHandlers -= value;
        }

        private void OnPeerDiscovered(EndPoint discoveredPeer)
        {
            var handlers = peerDiscoveredHandlers;
            if (handlers is not null)
                ThreadPool.QueueUserWorkItem<(EventHandler<EndPoint> Handler, object Sender, EndPoint Peer)>(static args => args.Handler(args.Sender, args.Peer), (handlers, this, discoveredPeer), false);
        }

        private async ValueTask AddPeerToActiveViewAsync(EndPoint peer, bool highPriority, CancellationToken token)
        {
            Debug.Assert(accessLock.IsWriteLockHeld);

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

        /// <summary>
        /// Gracefully shutdowns this peer.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result of the operation.</returns>
        public virtual async Task StopAsync(CancellationToken token)
        {
            PooledArrayBufferWriter<Task>? responses = null;
            await accessLock.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                responses = new(activeViewCapacity + 1);

                // notify all neighbors from active view
                foreach (var peer in activeView)
                {
                    responses.Add(Task.Run(async () =>
                    {
                        await DisconnectAsync(peer, false, token).ConfigureAwait(false);
                        await DisconnectAsync(peer).ConfigureAwait(false);
                    },
                    token));
                }

                // destroy all peers from passive view
                responses.Add(Task.Run(async () =>
                {
                    foreach (var peer in passiveView)
                        await DestroyAsync(peer).ConfigureAwait(false);
                },
                token));

                await Task.WhenAll(responses).ConfigureAwait(false);
            }
            finally
            {
                activeView = ImmutableHashSet<EndPoint>.Empty;
                passiveView = ImmutableHashSet<EndPoint>.Empty;
                accessLock.ExitWriteLock();
                responses?.Dispose();
            }

            await shuffleTask;
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // releases all local resources associated with the peer
                foreach (var peer in Interlocked.Exchange(ref activeView, ImmutableHashSet<EndPoint>.Empty))
                    Destroy(peer);

                foreach (var peer in Interlocked.Exchange(ref passiveView, ImmutableHashSet<EndPoint>.Empty))
                    Destroy(peer);

                lifecycleTokenSource.Dispose();
                accessLock.Dispose();
                peerDiscoveredHandlers = null;
                peerGoneHandlers = null;
            }

            base.Dispose(disposing);
        }

        /// <inheritdoc />
        protected override async ValueTask DisposeAsyncCore()
        {
            // releases all local resources associated with the peer
            foreach (var peer in Interlocked.Exchange(ref activeView, ImmutableHashSet<EndPoint>.Empty))
                await DestroyAsync(peer).ConfigureAwait(false);

            foreach (var peer in Interlocked.Exchange(ref passiveView, ImmutableHashSet<EndPoint>.Empty))
                await DestroyAsync(peer).ConfigureAwait(false);

            lifecycleTokenSource.Dispose();
            await accessLock.DisposeAsync().ConfigureAwait(false);

            peerGoneHandlers = null;
            peerDiscoveredHandlers = null;
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync() => DisposeAsync(false);
    }
}
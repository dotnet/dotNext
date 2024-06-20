using System.Collections.Immutable;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNext.Net.Cluster.Discovery.HyParView;

using Buffers;
using Collections.Generic;
using Collections.Specialized;
using Runtime.CompilerServices;
using static Threading.LinkedTokenSourceFactory;

/// <summary>
/// Represents local peer supporting HyParView membership
/// protocol for Gossip-based broadcast.
/// </summary>
/// <remarks>
/// This controller implements core logic of HyParView algorithm without transport-specific details.
/// </remarks>
/// <seealso href="https://asc.di.fct.unl.pt/~jleitao/pdf/dsn07-leitao.pdf">HyParView: a membership protocol for reliable gossip-based broadcast</seealso>
public abstract partial class PeerController : Disposable, IPeerMesh, IAsyncDisposable
{
    private readonly int activeViewCapacity, passiveViewCapacity;
    private readonly int activeRandomWalkLength, passiveRandomWalkLength, shuffleRandomWalkLength;
    private readonly int shuffleActiveViewCount, shufflePassiveViewCount;
    private readonly TimeSpan? shufflePeriod;
    private readonly Random random;
    private readonly CancellationTokenSource lifecycleTokenSource;
    private readonly Channel<Command> queue;
    private ImmutableHashSet<EndPoint> activeView, passiveView;
    private InvocationList<Action<PeerController, PeerEventArgs>> peerDiscoveredHandlers, peerGoneHandlers;
    private Task queueLoopTask;

    /// <summary>
    /// Initializes a new HyParView protocol controller.
    /// </summary>
    /// <param name="configuration">The configuration of the algorithm.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see name="IPeerConfiguration.ActiveViewCapacity"/> is less than or equal to 1;
    /// or <see name="IPeerConfiguration.PassiveViewCapacity"/> is less than <see name="IPeerConfiguration.ActiveViewCapacity"/>;
    /// or <see name="IPeerConfiguration.ActiveRandomWalkLength"/> is less than or equal to zero;
    /// or <see name="IPeerConfiguration.PassiveRandomWalkLength"/> is greater than <see name="IPeerConfiguration.ActiveRandomWalkLength"/>;
    /// or <see name="IPeerConfiguration.PassiveRandomWalkLength"/> is less than or equal to zero.
    /// </exception>
    protected PeerController(IPeerConfiguration configuration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(configuration.ActiveViewCapacity, 1, nameof(configuration));
        ArgumentOutOfRangeException.ThrowIfLessThan(configuration.PassiveViewCapacity, configuration.ActiveViewCapacity, nameof(configuration));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(configuration.ActiveRandomWalkLength, nameof(configuration));
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)configuration.PassiveRandomWalkLength, (uint)configuration.ActiveRandomWalkLength, nameof(configuration));

        activeViewCapacity = configuration.ActiveViewCapacity;
        passiveViewCapacity = configuration.PassiveViewCapacity;
        activeRandomWalkLength = configuration.ActiveRandomWalkLength;
        passiveRandomWalkLength = configuration.PassiveRandomWalkLength;
        shuffleActiveViewCount = configuration.ShuffleActiveViewCount;
        shufflePassiveViewCount = configuration.ShufflePassiveViewCount;
        shuffleRandomWalkLength = configuration.ShuffleRandomWalkLength;
        shufflePeriod = configuration.ShufflePeriod;
        random = new();
        queue = Channel.CreateBounded<Command>(new BoundedChannelOptions(configuration.QueueCapacity) { FullMode = BoundedChannelFullMode.Wait });
        PeerComparer = configuration.EndPointComparer ?? EqualityComparer<EndPoint>.Default;
        activeView = ImmutableHashSet.Create(PeerComparer);
        passiveView = ImmutableHashSet.Create(PeerComparer);
        lifecycleTokenSource = new();
        LifecycleToken = lifecycleTokenSource.Token;
        shuffleTask = Task.CompletedTask;
        queueLoopTask = Task.CompletedTask;
    }

    /// <summary>
    /// Gets peer address comparer.
    /// </summary>
    protected IEqualityComparer<EndPoint> PeerComparer { get; }

    /// <summary>
    /// Gets the logger associated with this controller.
    /// </summary>
    [CLSCompliant(false)]
    protected virtual ILogger Logger => NullLogger.Instance;

    /// <summary>
    /// Gets the token associated with the lifecycle of this object.
    /// </summary>
    protected CancellationToken LifecycleToken { get; }

    /// <summary>
    /// Gets a collection of discovered peers.
    /// </summary>
    public IReadOnlySet<EndPoint> Neighbors => activeView;

    /// <inheritdoc />
    IReadOnlySet<EndPoint> IPeerMesh.Peers => Neighbors;

    /// <summary>
    /// Starts serving HyParView messages and join to the cluster.
    /// </summary>
    /// <param name="contactNode">The contact node used to announce the current peer.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result of the operation.</returns>
    public async Task StartAsync(EndPoint? contactNode, CancellationToken token)
    {
        if (contactNode is not null)
            await JoinAsync(contactNode, token).ConfigureAwait(false);

        queueLoopTask = CommandLoop();

        if (shufflePeriod.TryGetValue(out var period))
            shuffleTask = ShuffleLoopAsync(period);
    }

    private async ValueTask EnqueueAsync(Command command, CancellationToken token)
    {
        var tokenSource = token.LinkTo(LifecycleToken);
        try
        {
            await queue.Writer.WriteAsync(command, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException e) when (tokenSource?.Token == e.CancellationToken)
        {
            throw new OperationCanceledException(e.Message, e, tokenSource.CancellationOrigin);
        }
        finally
        {
            tokenSource?.Dispose();
        }
    }

    private async Task CommandLoop()
    {
        var reader = queue.Reader;

        do
        {
            while (reader.TryRead(out var command))
            {
                try
                {
                    await ExecuteCommandAsync(in command).ConfigureAwait(false);
                }
                catch (OperationCanceledException e) when (e.CancellationToken == LifecycleToken)
                {
                    break;
                }
                catch (Exception e)
                {
                    Logger.FailedToProcessCommand((int)command.Type, e);
                }
                finally
                {
                    command = default;
                }
            }
        }
        while (await reader.WaitToReadAsync(LifecycleToken).ConfigureAwait(false));
    }

    private Task ExecuteCommandAsync(in Command command)
    {
        Task result;

        switch (command.Type)
        {
            default:
                result = Task.CompletedTask;
                break;
            case CommandType.Join:
                command.Join(out var sender);
                result = ProcessJoinAsync(sender);
                break;
            case CommandType.ForwardJoin:
                command.ForwardJoin(out sender, out var origin, out var ttl);
                result = ProcessForwardJoinAsync(sender, origin, ttl);
                break;
            case CommandType.Neighbor:
                command.Neighbor(out sender, out var highPriority);
                result = ProcessNeighborAsync(sender, highPriority);
                break;
            case CommandType.Disconnect:
                command.Disconnect(out sender, out var isAlive);
                result = ProcessDisconnectAsync(sender, isAlive);
                break;
            case CommandType.ShuffleReply:
                command.ShuffleReply(out var peers);
                result = ProcessShuffleReply(peers);
                break;
            case CommandType.Shuffle:
                command.Shuffle(out sender, out origin, out peers, out ttl);
                result = ProcessShuffleAsync(sender, origin, peers, ttl);
                break;
            case CommandType.ForceShuffle:
                result = ProcessShuffleAsync();
                break;
            case CommandType.Broadcast:
                command.Broadcast(out var senderFactory);
                result = ProcessBroadcastAsync(senderFactory);
                break;
        }

        return result;
    }

    /// <summary>
    /// An event raised when the visible neighbor becomes unavailable.
    /// </summary>
    public event Action<PeerController, PeerEventArgs> PeerGone
    {
        add => peerGoneHandlers += value;
        remove => peerGoneHandlers -= value;
    }

    /// <inheritdoc />
    event Action<IPeerMesh, PeerEventArgs> IPeerMesh.PeerGone
    {
        add => peerGoneHandlers += value;
        remove => peerGoneHandlers -= value;
    }

    private void OnPeerGone(EndPoint peer)
    {
        var handlers = peerGoneHandlers;
        if (!handlers.IsEmpty)
            handlers.Invoke(this, PeerEventArgs.Create(peer));
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

    private ValueTask AddPeerToPassiveViewAsync(EndPoint peer)
    {
        var result = Optional.None<EndPoint>();
        if (activeView.Contains(peer) || passiveView.Contains(peer) || IsLocalNode(peer))
            goto exit;

        if (passiveView.Count >= passiveViewCapacity && random.Peek(passiveView).TryGet(out var removedPeer))
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
        var passiveViewCopy = passiveView.ToBuilder();

        foreach (var peer in peers)
        {
            if (activeView.Contains(peer) || passiveViewCopy.Contains(peer))
                continue;

            if (passiveViewCopy.Count >= passiveViewCapacity && random.Peek(passiveViewCopy).TryGet(out var removedPeer))
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
    public event Action<PeerController, PeerEventArgs> PeerDiscovered
    {
        add => peerDiscoveredHandlers += value;
        remove => peerDiscoveredHandlers -= value;
    }

    /// <inheritdoc />
    event Action<IPeerMesh, PeerEventArgs> IPeerMesh.PeerDiscovered
    {
        add => peerDiscoveredHandlers += value;
        remove => peerDiscoveredHandlers -= value;
    }

    private void OnPeerDiscovered(EndPoint discoveredPeer)
    {
        var handlers = peerDiscoveredHandlers;
        if (!handlers.IsEmpty)
            handlers.Invoke(this, PeerEventArgs.Create(discoveredPeer));
    }

    /// <summary>
    /// Determines whether the address of the local node is equal to the specified address.
    /// </summary>
    /// <param name="peer">The peer address to compare.</param>
    /// <returns><see langword="true"/> if <paramref name="peer"/> is a local node; otherwise, <see langword="false"/>.</returns>
    protected abstract bool IsLocalNode(EndPoint peer);

    private async Task AddPeerToActiveViewAsync(EndPoint peer, bool highPriority)
    {
        if (activeView.Contains(peer) || IsLocalNode(peer))
            return;

        passiveView = passiveView.Remove(peer);

        // allocate space in active view if it is full
        if (activeView.Count >= activeViewCapacity && random.Peek(activeView).TryGet(out var removedPeer))
        {
            activeView = activeView.Remove(removedPeer);
            try
            {
                await DisconnectAsync(removedPeer, true, LifecycleToken).ConfigureAwait(false);
                await DisconnectAsync(removedPeer).ConfigureAwait(false);
            }
            finally
            {
                OnPeerGone(removedPeer);
            }

            await AddPeerToPassiveViewAsync(removedPeer).ConfigureAwait(false);
        }

        await NeighborAsync(peer, highPriority, LifecycleToken).ConfigureAwait(false);
        activeView = activeView.Add(peer);
        OnPeerDiscovered(peer);
    }

    /// <summary>
    /// Gracefully shutdowns this peer.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result of the operation.</returns>
    public virtual async Task StopAsync(CancellationToken token)
    {
        // terminate loops
        queue.Writer.Complete();

        // wait for completion
        await shuffleTask.ConfigureAwait(false);
        await queueLoopTask.ConfigureAwait(false);

        lifecycleTokenSource.Cancel();

        PoolingArrayBufferWriter<Task>? responses = null;
        try
        {
            responses = new() { Capacity = activeViewCapacity + 1 };

            // notify all neighbors from active view
            foreach (var peer in activeView)
            {
                responses.Add(DisconnectOnStopAsync(peer, token));
            }

            // destroy all peers from passive view
            responses.Add(DisconnectPassiveView(token));

            await Task.WhenAll(responses).ConfigureAwait(false);
        }
        finally
        {
            activeView = ImmutableHashSet<EndPoint>.Empty;
            passiveView = ImmutableHashSet<EndPoint>.Empty;
            responses?.Dispose();
        }
    }

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task DisconnectOnStopAsync(EndPoint peer, CancellationToken token)
    {
        await DisconnectAsync(peer, isAlive: false, token).ConfigureAwait(false);
        await DisconnectAsync(peer).ConfigureAwait(false);
    }

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task DisconnectPassiveView(CancellationToken token)
    {
        foreach (var peer in passiveView)
            await DestroyAsync(peer).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            queue.Writer.TryComplete(new ObjectDisposedException(GetType().Name));

            // releases all local resources associated with the peer
            foreach (var peer in Interlocked.Exchange(ref activeView, ImmutableHashSet<EndPoint>.Empty))
                Destroy(peer);

            foreach (var peer in Interlocked.Exchange(ref passiveView, ImmutableHashSet<EndPoint>.Empty))
                Destroy(peer);

            lifecycleTokenSource.Dispose();
            peerDiscoveredHandlers = default;
            peerGoneHandlers = default;
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        queue.Writer.TryComplete(new ObjectDisposedException(GetType().Name));

        // releases all local resources associated with the peer
        foreach (var peer in Interlocked.Exchange(ref activeView, ImmutableHashSet<EndPoint>.Empty))
            await DestroyAsync(peer).ConfigureAwait(false);

        foreach (var peer in Interlocked.Exchange(ref passiveView, ImmutableHashSet<EndPoint>.Empty))
            await DestroyAsync(peer).ConfigureAwait(false);

        lifecycleTokenSource.Dispose();

        peerGoneHandlers = default;
        peerDiscoveredHandlers = default;
    }

    /// <inheritdoc />
    public new ValueTask DisposeAsync() => base.DisposeAsync();
}
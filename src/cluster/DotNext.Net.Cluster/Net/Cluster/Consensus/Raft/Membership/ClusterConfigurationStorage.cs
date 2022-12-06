using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using static System.Threading.Timeout;

namespace DotNext.Net.Cluster.Consensus.Raft.Membership;

using Buffers;
using IO;
using Threading;

/// <summary>
/// Represents base class for all implementations of cluster configuration storages.
/// </summary>
/// <typeparam name="TAddress">The type of the cluster member address.</typeparam>
public abstract class ClusterConfigurationStorage<TAddress> : Disposable, IClusterConfigurationStorage<TAddress>
    where TAddress : notnull
{
    /// <summary>
    /// The memory allocator.
    /// </summary>
    protected readonly MemoryAllocator<byte>? allocator;
    private readonly Random fingerprintSource;
    private readonly AsyncExclusiveLock accessLock;
    private protected ImmutableDictionary<ClusterMemberId, TAddress> activeCache, proposedCache;
    private volatile TaskCompletionSource activatedEvent;
    private Func<ClusterConfigurationEvent<TAddress>, CancellationToken, ValueTask>? handlers;

    private protected ClusterConfigurationStorage(MemoryAllocator<byte>? allocator)
    {
        this.allocator = allocator;
        fingerprintSource = new();
        activeCache = proposedCache = ImmutableDictionary<ClusterMemberId, TAddress>.Empty;
        activatedEvent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        accessLock = new();
    }

    private protected long GenerateFingerprint() => fingerprintSource.NextInt64();

    private protected void OnActivated() => Interlocked.Exchange(ref activatedEvent, new(TaskCreationOptions.RunContinuationsAsynchronously)).SetResult();

    /// <summary>
    /// Encodes the address to its binary representation.
    /// </summary>
    /// <param name="address">The address to encode.</param>
    /// <param name="output">The buffer for the address.</param>
    protected abstract void Encode(TAddress address, ref BufferWriterSlim<byte> output);

    private protected void Encode(IReadOnlyDictionary<ClusterMemberId, TAddress> configuration, ref BufferWriterSlim<byte> output)
    {
        output.WriteInt32(configuration.Count, true);

        foreach (var (id, address) in configuration)
        {
            // serialize id
#pragma warning disable CA2252  // TODO: Remove in .NET 7
            output.WriteFormattable(id);
#pragma warning restore CA2252

            // serialize address
            Encode(address, ref output);
        }
    }

    /// <summary>
    /// Decodes the address of the node from its binary representation.
    /// </summary>
    /// <param name="reader">The reader of binary data.</param>
    /// <returns>The decoded address.</returns>
    protected abstract TAddress Decode(ref SequenceReader reader);

    private void Decode(IDictionary<ClusterMemberId, TAddress> output, ref SequenceReader reader)
    {
        Span<byte> memberIdBuffer = stackalloc byte[ClusterMemberId.Size];

        for (var count = reader.ReadInt32(true); count > 0; count--)
        {
            // deserialize id
            reader.Read(memberIdBuffer);
            var id = new ClusterMemberId(memberIdBuffer);

            // deserialize address
            var address = Decode(ref reader);

            output.TryAdd(id, address);
        }
    }

    private protected void Decode(IDictionary<ClusterMemberId, TAddress> output, ReadOnlyMemory<byte> memory)
    {
        var reader = IAsyncBinaryReader.Create(memory);
        Decode(output, ref reader);
    }

    /// <summary>
    /// Represents active cluster configuration maintained by the node.
    /// </summary>
    public abstract IClusterConfiguration ActiveConfiguration { get; }

    /// <inheritdoc />
    IReadOnlyDictionary<ClusterMemberId, TAddress> IClusterConfigurationStorage<TAddress>.ActiveConfiguration
        => activeCache;

    /// <summary>
    /// Represents proposed cluster configuration.
    /// </summary>
    public abstract IClusterConfiguration? ProposedConfiguration { get; }

    /// <inheritdoc />
    IReadOnlyDictionary<ClusterMemberId, TAddress>? IClusterConfigurationStorage<TAddress>.ProposedConfiguration
        => HasProposal ? proposedCache : null;

    /// <summary>
    /// Proposes the configuration.
    /// </summary>
    /// <remarks>
    /// If method is called multiple times then <see cref="ProposedConfiguration"/> will be rewritten.
    /// </remarks>
    /// <param name="configuration">The proposed configuration.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    protected abstract ValueTask ProposeAsync(IClusterConfiguration configuration, CancellationToken token = default);

    /// <inheritdoc/>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    async ValueTask IClusterConfigurationStorage.ProposeAsync(IClusterConfiguration configuration, CancellationToken token)
    {
        await accessLock.AcquireAsync(token).ConfigureAwait(false);
        try
        {
            await ProposeAsync(configuration, token).ConfigureAwait(false);
        }
        finally
        {
            accessLock.Release();
        }
    }

    /// <summary>
    /// Applies proposed configuration as active configuration.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    protected abstract ValueTask ApplyAsync(CancellationToken token = default);

    /// <inheritdoc/>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    async ValueTask IClusterConfigurationStorage.ApplyAsync(CancellationToken token)
    {
        await accessLock.AcquireAsync(token).ConfigureAwait(false);
        try
        {
            await ApplyAsync(token).ConfigureAwait(false);
        }
        finally
        {
            accessLock.Release();
        }
    }

    /// <summary>
    /// Loads configuration from the storage.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    protected abstract ValueTask LoadConfigurationAsync(CancellationToken token = default);

    /// <inheritdoc/>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    async ValueTask IClusterConfigurationStorage.LoadConfigurationAsync(CancellationToken token)
    {
        await accessLock.AcquireAsync(token).ConfigureAwait(false);
        try
        {
            await LoadConfigurationAsync(token).ConfigureAwait(false);
        }
        finally
        {
            accessLock.Release();
        }
    }

    /// <summary>
    /// Gets a value indicating that this storage has proposed configuration.
    /// </summary>
    public abstract bool HasProposal { get; }

    /// <inheritdoc />
    Task IClusterConfigurationStorage.WaitForApplyAsync(CancellationToken token)
        => HasProposal ? activatedEvent.Task.WaitAsync(InfiniteTimeSpan, token) : Task.CompletedTask;

    /// <summary>
    /// Proposes a new member.
    /// </summary>
    /// <param name="id">The identifier of the cluster member to add.</param>
    /// <param name="address">The address of the cluster member.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the new member is added to the proposed configuration;
    /// <see langword="false"/> if the storage has the proposed configuration already.
    /// </returns>
    protected abstract ValueTask<bool> AddMemberAsync(ClusterMemberId id, TAddress address, CancellationToken token = default);

    /// <inheritdoc/>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    async ValueTask<bool> IClusterConfigurationStorage<TAddress>.AddMemberAsync(ClusterMemberId id, TAddress address, CancellationToken token)
    {
        await accessLock.AcquireAsync(token).ConfigureAwait(false);
        try
        {
            return await AddMemberAsync(id, address, token).ConfigureAwait(false);
        }
        finally
        {
            accessLock.Release();
        }
    }

    /// <summary>
    /// Proposes removal of the existing member.
    /// </summary>
    /// <param name="id">The identifier of the cluster member to remove.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the new member is added to the proposed configuration;
    /// <see langword="false"/> if the storage has the proposed configuration already.
    /// </returns>
    protected abstract ValueTask<bool> RemoveMemberAsync(ClusterMemberId id, CancellationToken token = default);

    /// <inheritdoc/>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    async ValueTask<bool> IClusterConfigurationStorage<TAddress>.RemoveMemberAsync(ClusterMemberId id, CancellationToken token)
    {
        await accessLock.AcquireAsync(token).ConfigureAwait(false);
        try
        {
            return await RemoveMemberAsync(id, token).ConfigureAwait(false);
        }
        finally
        {
            accessLock.Release();
        }
    }

    /// <summary>
    /// An event occurred when proposed configuration is applied.
    /// </summary>
    public event Func<ClusterConfigurationEvent<TAddress>, CancellationToken, ValueTask> ActiveConfigurationChanged
    {
        add => handlers += value;
        remove => handlers -= value;
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask OnActiveConfigurationChanged(ClusterConfigurationEvent<TAddress> ev, CancellationToken token)
    {
        foreach (Func<ClusterConfigurationEvent<TAddress>, CancellationToken, ValueTask> handler in handlers?.GetInvocationList() ?? Array.Empty<Func<ClusterConfigurationEvent<TAddress>, CancellationToken, ValueTask>>())
            await handler.Invoke(ev, token).ConfigureAwait(false);
    }

    private protected async ValueTask CompareAsync(IReadOnlyDictionary<ClusterMemberId, TAddress> active, IReadOnlyDictionary<ClusterMemberId, TAddress> proposed, CancellationToken token)
    {
        foreach (var (id, address) in active)
        {
            if (!proposed.ContainsKey(id))
                await OnActiveConfigurationChanged(new() { Id = id, Address = address, IsAdded = false }, token).ConfigureAwait(false);
        }

        foreach (var (id, address) in proposed)
        {
            if (!active.ContainsKey(id))
                await OnActiveConfigurationChanged(new() { Id = id, Address = address, IsAdded = true }, token).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            activeCache = activeCache.Clear();
            proposedCache = proposedCache.Clear();
            handlers = null;
            accessLock.Dispose();
        }

        base.Dispose(disposing);
    }
}
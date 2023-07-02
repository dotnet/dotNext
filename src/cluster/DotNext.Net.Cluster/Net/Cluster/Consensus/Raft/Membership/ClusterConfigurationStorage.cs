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
    private const string ConfigurationTypeMeterAttributeName = "dotnext.raft.config.type";

    /// <summary>
    /// The memory allocator.
    /// </summary>
    protected readonly MemoryAllocator<byte>? allocator;
    private readonly Random fingerprintSource;
    private readonly AsyncExclusiveLock accessLock;
    private protected ImmutableHashSet<TAddress> activeCache, proposedCache;
    private volatile TaskCompletionSource activatedEvent;
    private Func<TAddress, bool, CancellationToken, ValueTask>? handlers;

    private protected ClusterConfigurationStorage(IEqualityComparer<TAddress>? comparer, MemoryAllocator<byte>? allocator)
    {
        this.allocator = allocator;
        fingerprintSource = new();
        activeCache = proposedCache = ImmutableHashSet.Create<TAddress>(comparer);
        activatedEvent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        accessLock = new()
        {
            MeasurementTags = new()
            {
                { ConfigurationTypeMeterAttributeName, GetType().Name },
            },
        };
    }

    private protected long GenerateFingerprint() => fingerprintSource.NextInt64();

    private protected void OnActivated() => Interlocked.Exchange(ref activatedEvent, new(TaskCreationOptions.RunContinuationsAsynchronously)).SetResult();

    /// <summary>
    /// Encodes the address to its binary representation.
    /// </summary>
    /// <param name="address">The address to encode.</param>
    /// <param name="output">The buffer for the address.</param>
    protected abstract void Encode(TAddress address, ref BufferWriterSlim<byte> output);

    private protected void Encode(IReadOnlyCollection<TAddress> configuration, ref BufferWriterSlim<byte> output)
    {
        output.WriteInt32(configuration.Count, true);

        foreach (var address in configuration)
        {
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

    private void Decode(ICollection<TAddress> output, ref SequenceReader reader)
    {
        for (var count = reader.ReadInt32(true); count > 0; count--)
        {
            output.Add(Decode(ref reader));
        }
    }

    private protected void Decode(ICollection<TAddress> output, ReadOnlyMemory<byte> memory)
    {
        var reader = IAsyncBinaryReader.Create(memory);
        Decode(output, ref reader);
    }

    /// <summary>
    /// Represents active cluster configuration maintained by the node.
    /// </summary>
    public abstract IClusterConfiguration ActiveConfiguration { get; }

    /// <inheritdoc />
    IReadOnlySet<TAddress> IClusterConfigurationStorage<TAddress>.ActiveConfiguration
        => activeCache;

    /// <summary>
    /// Represents proposed cluster configuration.
    /// </summary>
    public abstract IClusterConfiguration? ProposedConfiguration { get; }

    /// <inheritdoc />
    IReadOnlySet<TAddress>? IClusterConfigurationStorage<TAddress>.ProposedConfiguration
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
    /// <param name="address">The address of the cluster member.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the new member is added to the proposed configuration;
    /// <see langword="false"/> if the storage has the proposed configuration already.
    /// </returns>
    protected abstract ValueTask<bool> AddMemberAsync(TAddress address, CancellationToken token = default);

    /// <inheritdoc/>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    async ValueTask<bool> IClusterConfigurationStorage<TAddress>.AddMemberAsync(TAddress address, CancellationToken token)
    {
        await accessLock.AcquireAsync(token).ConfigureAwait(false);
        try
        {
            return await AddMemberAsync(address, token).ConfigureAwait(false);
        }
        finally
        {
            accessLock.Release();
        }
    }

    /// <summary>
    /// Proposes removal of the existing member.
    /// </summary>
    /// <param name="address">The address of the cluster member.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the new member is added to the proposed configuration;
    /// <see langword="false"/> if the storage has the proposed configuration already.
    /// </returns>
    protected abstract ValueTask<bool> RemoveMemberAsync(TAddress address, CancellationToken token = default);

    /// <inheritdoc/>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    async ValueTask<bool> IClusterConfigurationStorage<TAddress>.RemoveMemberAsync(TAddress address, CancellationToken token)
    {
        await accessLock.AcquireAsync(token).ConfigureAwait(false);
        try
        {
            return await RemoveMemberAsync(address, token).ConfigureAwait(false);
        }
        finally
        {
            accessLock.Release();
        }
    }

    /// <summary>
    /// An event occurred when proposed configuration is applied.
    /// </summary>
    public event Func<TAddress, bool, CancellationToken, ValueTask> ActiveConfigurationChanged
    {
        add => handlers += value;
        remove => handlers -= value;
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask OnActiveConfigurationChanged(TAddress address, bool isAdded, CancellationToken token)
    {
        foreach (Func<TAddress, bool, CancellationToken, ValueTask> handler in handlers?.GetInvocationList() ?? Array.Empty<Func<TAddress, bool, CancellationToken, ValueTask>>())
            await handler.Invoke(address, isAdded, token).ConfigureAwait(false);
    }

    private protected async ValueTask CompareAsync(IReadOnlySet<TAddress> active, IReadOnlySet<TAddress> proposed, CancellationToken token)
    {
        foreach (var address in active)
        {
            if (!proposed.Contains(address))
                await OnActiveConfigurationChanged(address, isAdded: false, token).ConfigureAwait(false);
        }

        foreach (var address in proposed)
        {
            if (!active.Contains(address))
                await OnActiveConfigurationChanged(address, isAdded: true, token).ConfigureAwait(false);
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
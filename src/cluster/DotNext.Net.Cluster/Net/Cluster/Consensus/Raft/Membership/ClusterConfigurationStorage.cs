using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft.Membership;

using Buffers;
using IO;
using Threading;

/// <summary>
/// Represents base class for all implementations of cluster configuration storages.
/// </summary>
/// <typeparam name="TAddress">The type of the cluster member address.</typeparam>
public abstract partial class ClusterConfigurationStorage<TAddress> : Disposable, IClusterConfigurationStorage<TAddress>
    where TAddress : notnull
{
    private const int BufferSize = 10 * 1024; // 10KB
    private const string ConfigurationTypeMeterAttributeName = "dotnext.raft.config.type";

    private readonly AsyncReaderWriterLock accessLock;
    private readonly MemoryAllocator<byte> allocator;
    private Func<IClusterConfiguration<TAddress>, CancellationToken, ValueTask>? handlers;

    private protected ClusterConfigurationStorage()
    {
        allocator = MemoryAllocator<byte>.Default;
        accessLock = new()
        {
            MeasurementTags = new()
            {
                { ConfigurationTypeMeterAttributeName, GetType().Name },
            },
        };
    }

    /// <summary>
    /// Gets the comparer of the address.
    /// </summary>
    protected virtual IEqualityComparer<TAddress> Comparer => EqualityComparer<TAddress>.Default;

    /// <summary>
    /// Gets or sets memory allocator.
    /// </summary>
    [AllowNull]
    public MemoryAllocator<byte> MemoryAllocator
    {
        get => allocator;
        init => allocator = value.DefaultIfNull;
    }

    /// <summary>
    /// Encodes the address to its binary representation.
    /// </summary>
    /// <param name="address">The address to encode.</param>
    /// <param name="writer">The buffer for the address.</param>
    protected abstract void Encode(TAddress address, ref BufferWriterSlim<byte> writer);

    /// <summary>
    /// Decodes the address of the node from its binary representation.
    /// </summary>
    /// <param name="reader">The reader of binary data.</param>
    /// <returns>The decoded address.</returns>
    protected abstract TAddress Decode(ref SequenceReader reader);

    /// <summary>
    /// Loads the configuration from the storage.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The memory block representing the cluster configuration.</returns>
    protected abstract ValueTask<(MemoryOwner<byte> Configuration, long Version)> LoadConfigurationAsync(CancellationToken token);

    /// <inheritdoc />
    async ValueTask<IClusterConfiguration<TAddress>> IClusterConfigurationStorage<TAddress>.LoadConfigurationAsync(CancellationToken token)
    {
        var owner = default(MemoryOwner<byte>);
        await accessLock.EnterReadLockAsync(token).ConfigureAwait(false);
        try
        {
            (owner, _) = await LoadConfigurationAsync(token).ConfigureAwait(false);
            return Deserialize(owner.Memory);
        }
        finally
        {
            accessLock.Release();
            owner.Dispose();
        }
    }

    /// <inheritdoc />
    async ValueTask<(IDataTransferObject Configuration, long Version)> IClusterConfigurationStorage.LoadConfigurationAsync(CancellationToken token)
    {
        (IDataTransferObject Configuration, long Version) result = default;
        var owner = default(MemoryOwner<byte>);
        await accessLock.EnterReadLockAsync(token).ConfigureAwait(false);
        try
        {
            (owner, result.Version) = await LoadConfigurationAsync(token).ConfigureAwait(false);
            result.Configuration = Deserialize(owner.Memory);
        }
        finally
        {
            accessLock.Release();
            owner.Dispose();
        }

        return result;
    }

    /// <summary>
    /// Stores the configuration.
    /// </summary>
    /// <param name="configuration">The memory block representing the cluster configuration.</param>
    /// <param name="configurationVersion">The version of the configuration.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    protected abstract ValueTask<bool> SaveConfigurationAsync(ReadOnlyMemory<byte> configuration, long configurationVersion, CancellationToken token);

    /// <inheritdoc />
    ValueTask<bool> IClusterConfigurationStorage.SaveConfigurationAsync<TConfiguration>(TConfiguration configuration, long configurationVersion,
        CancellationToken token)
        => configuration.TryGetMemory(out var payload)
            ? SaveRawConfigurationAsync(payload, configurationVersion, token)
            : CopyAndSaveConfigurationAsync(configuration, configurationVersion, token);

    private async ValueTask<bool> SaveRawConfigurationAsync(ReadOnlyMemory<byte> configuration, long configurationVersion, CancellationToken token)
    {
        bool installed;
        await accessLock.EnterWriteLockAsync(token).ConfigureAwait(false);
        try
        {
            installed = await SaveConfigurationAsync(configuration, configurationVersion, token).ConfigureAwait(false);
            if (installed)
                await InvokeHandlers(Deserialize(configuration), handlers, token).ConfigureAwait(false);
        }
        finally
        {
            accessLock.Release();
        }

        return installed;
    }

    private async ValueTask<bool> CopyAndSaveConfigurationAsync<TConfiguration>(TConfiguration configuration, long configurationVersion,
        CancellationToken token)
        where TConfiguration : IDataTransferObject
    {
        bool installed;
        var writer = new PoolingBufferWriter<byte>(allocator) { Capacity = BufferSize };
        await accessLock.EnterWriteLockAsync(token).ConfigureAwait(false);
        try
        {
            await configuration.WriteToAsync(writer, token).ConfigureAwait(false);
            installed = await SaveConfigurationAsync(writer.WrittenMemory, configurationVersion, token).ConfigureAwait(false);
            if (installed)
                await InvokeHandlers(Deserialize(writer.WrittenMemory), handlers, token).ConfigureAwait(false);
        }
        finally
        {
            accessLock.Release();
        }

        return installed;
    }

    private static async ValueTask InvokeHandlers(IClusterConfiguration<TAddress> configuration,
        Func<IClusterConfiguration<TAddress>, CancellationToken, ValueTask>? handlers, CancellationToken token)
    {
        foreach (var handler in Delegate.EnumerateInvocationList(handlers))
        {
            await handler(configuration, token).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public event Func<IClusterConfiguration<TAddress>, CancellationToken, ValueTask> ConfigurationChanged
    {
        add => handlers += value;
        remove => handlers -= value;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            accessLock.Dispose();
        }

        base.Dispose(disposing);
    }
}
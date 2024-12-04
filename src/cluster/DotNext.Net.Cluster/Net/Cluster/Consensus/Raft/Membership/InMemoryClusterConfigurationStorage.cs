using System.Collections.Immutable;
using System.Net;

namespace DotNext.Net.Cluster.Consensus.Raft.Membership;

using Buffers;
using IO;

/// <summary>
/// Represents in-memory storage of cluster configuration.
/// </summary>
/// <typeparam name="TAddress">The type of cluster member address.</typeparam>
public abstract class InMemoryClusterConfigurationStorage<TAddress> : ClusterConfigurationStorage<TAddress>
    where TAddress : notnull
{
    private const int InitialBufferSize = 512;

    private sealed class ClusterConfiguration : Disposable, IClusterConfiguration
    {
        private MemoryOwner<byte> payload;

        internal ClusterConfiguration(long fingerprint, MemoryOwner<byte> configuration)
        {
            payload = configuration;
            Fingerprint = fingerprint;
        }

        public long Fingerprint { get; }

        public long Length => payload.Length;

        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => writer.WriteAsync(payload.Memory, null, token);

        bool IDataTransferObject.IsReusable => true;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                payload.Dispose();

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Represents configuration builder.
    /// </summary>
    public sealed class ConfigurationBuilder : HashSet<TAddress>
    {
        private readonly InMemoryClusterConfigurationStorage<TAddress> storage;

        internal ConfigurationBuilder(InMemoryClusterConfigurationStorage<TAddress> storage)
            : base(storage.activeCache, storage.activeCache.KeyComparer)
            => this.storage = storage;

        /// <summary>
        /// Builds active configuration.
        /// </summary>
        public void Build()
        {
            storage.activeCache = ImmutableHashSet.CreateRange(Comparer, this);

            var config = storage.Encode(this);
            storage.active?.Dispose();
            storage.active = new(storage.GenerateFingerprint(), config);
        }
    }

    private ClusterConfiguration? active, proposed;

    /// <summary>
    /// Initializes a new in-memory configuration storage.
    /// </summary>
    /// <param name="comparer">An object responsible for comparison of <typeparamref name="TAddress"/> values.</param>
    /// <param name="allocator">The memory allocator.</param>
    protected InMemoryClusterConfigurationStorage(IEqualityComparer<TAddress>? comparer = null, MemoryAllocator<byte>? allocator = null)
        : base(comparer, allocator)
    {
    }

    /// <inheritdoc />
    public sealed override bool HasProposal => proposed is not null;

    /// <summary>
    /// Gets active configuration.
    /// </summary>
    public sealed override IClusterConfiguration ActiveConfiguration
        => active ??= new(0L, Encode(activeCache));

    /// <summary>
    /// Gets proposed configuration.
    /// </summary>
    public sealed override IClusterConfiguration? ProposedConfiguration => proposed;

    /// <inheritdoc/>
    protected sealed override async ValueTask ProposeAsync(IClusterConfiguration configuration, CancellationToken token = default)
    {
        var config = await configuration.ToMemoryAsync(allocator, token).ConfigureAwait(false);

        proposed?.Dispose();
        proposed = new(configuration.Fingerprint, config);

        proposedCache.Clear();
        var builder = proposedCache.ToBuilder();
        Decode(builder, config.Memory);
        proposedCache = builder.ToImmutable();
        Interlocked.MemoryBarrierProcessWide();
    }

    /// <inheritdoc />
    protected sealed override ValueTask ApplyAsync(CancellationToken token)
        => proposed is null ? ValueTask.CompletedTask : ApplyProposedAsync(token);

    private async ValueTask ApplyProposedAsync(CancellationToken token)
    {
        await CompareAsync(activeCache, proposedCache, token).ConfigureAwait(false);

        active?.Dispose();
        active = proposed;
        activeCache = proposedCache;

        proposed = null;
        proposedCache = proposedCache.Clear();

        Interlocked.MemoryBarrierProcessWide();
        OnActivated();
    }

    private MemoryOwner<byte> Encode(IReadOnlyCollection<TAddress> configuration)
    {
        MemoryOwner<byte> result;
        var writer = new BufferWriterSlim<byte>(InitialBufferSize, allocator);

        try
        {
            Encode(configuration, ref writer);

            result = writer.DetachOrCopyBuffer();
        }
        finally
        {
            writer.Dispose();
        }

        return result;
    }

    /// <summary>
    /// Proposes a new member.
    /// </summary>
    /// <param name="address">The address of the cluster member.</param>
    /// <returns>
    /// <see langword="true"/> if the new member is added to the proposed configuration;
    /// <see langword="false"/> if the storage has the proposed configuration already.
    /// </returns>
    public bool AddMember(TAddress address)
    {
        if (proposed is not null || activeCache.Contains(address))
            return false;

        var builder = activeCache.ToBuilder();
        builder.Add(address);
        proposedCache = builder.ToImmutable();

        proposed = new(GenerateFingerprint(), Encode(builder));
        Interlocked.MemoryBarrierProcessWide();
        builder.Clear();

        return true;
    }

    /// <inheritdoc />
    protected sealed override ValueTask<bool> AddMemberAsync(TAddress address, CancellationToken token = default)
    {
        ValueTask<bool> result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled<bool>(token);
        }
        else
        {
            try
            {
                result = new(AddMember(address));
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<bool>(e);
            }
        }

        return result;
    }

    /// <summary>
    /// Proposes removal of the existing member.
    /// </summary>
    /// <param name="address">The address of the cluster member.</param>
    /// <returns>
    /// <see langword="true"/> if the new member is added to the proposed configuration;
    /// <see langword="false"/> if the storage has the proposed configuration already.
    /// </returns>
    public bool RemoveMember(TAddress address)
    {
        if (proposed is not null)
            return false;

        var builder = activeCache.ToBuilder();
        if (!builder.Remove(address))
            return false;
        proposedCache = builder.ToImmutable();

        proposed = new(GenerateFingerprint(), Encode(builder));
        Interlocked.MemoryBarrierProcessWide();
        builder.Clear();

        return true;
    }

    /// <inheritdoc />
    protected sealed override ValueTask<bool> RemoveMemberAsync(TAddress address, CancellationToken token = default)
    {
        ValueTask<bool> result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled<bool>(token);
        }
        else
        {
            try
            {
                result = new(RemoveMember(address));
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<bool>(e);
            }
        }

        return result;
    }

    /// <summary>
    /// Creates a builder that can be used to initialize active configuration.
    /// </summary>
    /// <returns>A builder of active configuration.</returns>
    public ConfigurationBuilder CreateActiveConfigurationBuilder() => new(this);

    /// <inheritdoc />
    protected sealed override ValueTask LoadConfigurationAsync(CancellationToken token = default)
        => ValueTask.CompletedTask;

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            active?.Dispose();
            active = null;

            proposed?.Dispose();
            proposed = null;
        }

        base.Dispose(disposing);
    }
}

internal sealed class InMemoryClusterConfigurationStorage : InMemoryClusterConfigurationStorage<EndPoint>
{
    internal InMemoryClusterConfigurationStorage(IEqualityComparer<EndPoint> comparer, MemoryAllocator<byte>? allocator)
        : base(comparer, allocator)
    {
    }

    protected override void Encode(EndPoint address, ref BufferWriterSlim<byte> output)
        => output.WriteEndPoint(address);

    protected override EndPoint Decode(ref SequenceReader reader)
        => reader.ReadEndPoint();
}
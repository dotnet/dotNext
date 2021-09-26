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
    public sealed class ConfigurationBuilder : Dictionary<ClusterMemberId, TAddress>
    {
        private readonly InMemoryClusterConfigurationStorage<TAddress> storage;

        internal ConfigurationBuilder(InMemoryClusterConfigurationStorage<TAddress> storage)
            : base(storage.activeCache)
            => this.storage = storage;

        /// <summary>
        /// Builds active configuration.
        /// </summary>
        public void Build()
        {
            storage.activeCache = ImmutableDictionary.CreateRange(this);

            var config = storage.Encode(this);
            storage.active?.Dispose();
            storage.active = new(storage.GenerateFingerprint(), config);
        }
    }

    private ClusterConfiguration? active, proposed;

    /// <summary>
    /// Initializes a new in-memory configuration storage.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    protected InMemoryClusterConfigurationStorage(MemoryAllocator<byte>? allocator = null)
        : base(10, allocator)
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

    /// <summary>
    /// Proposes the configuration.
    /// </summary>
    /// <param name="configuration">The proposed configuration.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    public sealed override async ValueTask ProposeAsync(IClusterConfiguration configuration, CancellationToken token = default)
    {
        var config = await configuration.ToMemoryAsync(allocator, token).ConfigureAwait(false);

        proposed?.Dispose();
        proposed = new(configuration.Fingerprint, config);

        proposedCache.Clear();
        var builder = proposedCache.ToBuilder();
        Decode(builder, config.Memory);
        proposedCache = builder.ToImmutable();
    }

    /// <inheritdoc />
    public sealed override async ValueTask ApplyAsync(CancellationToken token)
    {
        if (proposed is null)
            return;

        await CompareAsync(activeCache, proposedCache).ConfigureAwait(false);

        active?.Dispose();
        active = proposed;
        activeCache = proposedCache;

        proposed = null;
        proposedCache = proposedCache.Clear();

        OnActivated();
    }

    private MemoryOwner<byte> Encode(IReadOnlyDictionary<ClusterMemberId, TAddress> configuration)
    {
        MemoryOwner<byte> result;
        var writer = new BufferWriterSlim<byte>(InitialBufferSize, allocator);

        try
        {
            Encode(configuration, ref writer);

            if (!writer.TryDetachBuffer(out result))
                result = writer.WrittenSpan.Copy(allocator);
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
    /// <param name="id">The identifier of the cluster member to add.</param>
    /// <param name="address">The address of the cluster member.</param>
    /// <returns>
    /// <see langword="true"/> if the new member is added to the proposed configuration;
    /// <see langword="false"/> if the storage has the proposed configuration already.
    /// </returns>
    public bool AddMember(ClusterMemberId id, TAddress address)
    {
        if (proposed is not null || activeCache.ContainsKey(id))
            return false;

        var builder = activeCache.ToBuilder();
        builder.Add(id, address);
        proposedCache = builder.ToImmutable();

        proposed = new(GenerateFingerprint(), Encode(builder));
        builder.Clear();

        return true;
    }

    /// <inheritdoc />
    public sealed override ValueTask<bool> AddMemberAsync(ClusterMemberId id, TAddress address, CancellationToken token = default)
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
                result = new(AddMember(id, address));
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
    /// <param name="id">The identifier of the cluster member to remove.</param>
    /// <returns>
    /// <see langword="true"/> if the new member is added to the proposed configuration;
    /// <see langword="false"/> if the storage has the proposed configuration already.
    /// </returns>
    public bool RemoveMember(ClusterMemberId id)
    {
        if (proposed is not null || !activeCache.ContainsKey(id))
            return false;

        var builder = activeCache.ToBuilder();
        if (!builder.Remove(id, out var address))
            return false;
        proposedCache = builder.ToImmutable();

        proposed = new(GenerateFingerprint(), Encode(builder));
        builder.Clear();

        return true;
    }

    /// <inheritdoc />
    public sealed override ValueTask<bool> RemoveMemberAsync(ClusterMemberId id, CancellationToken token = default)
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
                result = new(RemoveMember(id));
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

internal sealed class InMemoryClusterConfigurationStorage : InMemoryClusterConfigurationStorage<IPEndPoint>
{
    internal InMemoryClusterConfigurationStorage(MemoryAllocator<byte>? allocator)
        : base(allocator)
    {
    }

    protected override void Encode(IPEndPoint address, ref BufferWriterSlim<byte> output)
        => output.WriteEndPoint(address);

    protected override IPEndPoint Decode(ref SequenceReader reader)
        => (IPEndPoint)reader.ReadEndPoint();
}
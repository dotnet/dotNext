using System.Net;

namespace DotNext.Net.Cluster.Consensus.Raft.Membership;

using Buffers;

/// <summary>
/// Represents in-memory storage of cluster configuration.
/// </summary>
/// <typeparam name="TAddress">The type of cluster member address.</typeparam>
public abstract class InMemoryClusterConfigurationStorage<TAddress> : ClusterConfigurationStorage<TAddress>
    where TAddress : notnull
{
    private long version;
    private byte[] snapshot;
    
    /// <summary>
    /// Initializes a new in-memory configuration storage.
    /// </summary>
    protected InMemoryClusterConfigurationStorage()
    {
        snapshot = [];
    }

    /// <summary>
    /// Creates a builder of the initial configuration.
    /// </summary>
    /// <returns>The builder instance.</returns>
    public Builder CreateInitialConfigurationBuilder() => new(this);

    /// <inheritdoc />
    protected sealed override ValueTask<(MemoryOwner<byte> Configuration, long Version)> LoadConfigurationAsync(CancellationToken token)
        => ValueTask.FromResult<(MemoryOwner<byte> Configuration, long Version)>(new()
        {
            Configuration = new(snapshot),
            Version = version,
        });

    /// <inheritdoc />
    protected sealed override ValueTask<bool> SaveConfigurationAsync(ReadOnlyMemory<byte> configuration, long configurationVersion,
        CancellationToken token)
    {
        ValueTask<bool> changed;
        if (configurationVersion > version || snapshot is [])
        {
            version = configurationVersion;
            snapshot = configuration.ToArray();
            changed = ValueTask.FromResult(true);
        }
        else
        {
            changed = ValueTask.FromResult(false);
        }

        return changed;
    }

    /// <summary>
    /// Represents the configuration builder.
    /// </summary>
    public sealed class Builder : HashSet<TAddress>
    {
        private readonly InMemoryClusterConfigurationStorage<TAddress> storage;

        internal Builder(InMemoryClusterConfigurationStorage<TAddress> storage)
            : base(storage.Comparer)
            => this.storage = storage;

        /// <summary>
        /// Builds the configuration.
        /// </summary>
        public void Build()
        {
            var owner = storage.Serialize(this);
            try
            {
                storage.snapshot = owner.Span.ToArray();
            }
            finally
            {
                owner.Dispose();
            }
        }
    }
}

internal sealed class InMemoryClusterConfigurationStorage(IEqualityComparer<EndPoint> comparer) : InMemoryClusterConfigurationStorage<EndPoint>
{
    protected override void Encode(EndPoint address, ref BufferWriterSlim<byte> writer)
        => writer.WriteEndPoint(address);

    protected override EndPoint Decode(ref SequenceReader reader)
        => reader.ReadEndPoint();

    protected override IEqualityComparer<EndPoint> Comparer => comparer;
}
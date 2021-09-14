using System.Collections.Immutable;
using System.Threading.Channels;
using static System.Threading.Timeout;

namespace DotNext.Net.Cluster.Consensus.Raft.Membership
{
    using Buffers;
    using IO;

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
        private readonly Channel<ClusterConfigurationEvent<TAddress>> events;
        private protected ImmutableDictionary<ClusterMemberId, TAddress> activeCache, proposedCache;
        private volatile TaskCompletionSource activatedEvent;

        private protected ClusterConfigurationStorage(int eventQueueCapacity, MemoryAllocator<byte>? allocator)
        {
            this.allocator = allocator;
            fingerprintSource = new();
            activeCache = proposedCache = ImmutableDictionary<ClusterMemberId, TAddress>.Empty;
            events = Channel.CreateBounded<ClusterConfigurationEvent<TAddress>>(new BoundedChannelOptions(eventQueueCapacity) { FullMode = BoundedChannelFullMode.Wait });
            activatedEvent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private protected long GenerateFingerprint() => fingerprintSource.Next<long>();

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
                output.WriteFormattable(id);

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
        public abstract ValueTask ProposeAsync(IClusterConfiguration configuration, CancellationToken token = default);

        /// <summary>
        /// Applies proposed configuration as active configuration.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result.</returns>
        public abstract ValueTask ApplyAsync(CancellationToken token = default);

        /// <summary>
        /// Loads configuration from the storage.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result.</returns>
        public virtual async ValueTask LoadConfigurationAsync(CancellationToken token = default)
        {
            // enumerate all entries in active config and raise events
            foreach (var (id, address) in activeCache)
                await events.Writer.WriteAsync(new() { Id = id, Address = address, IsAdded = true }, token).ConfigureAwait(false);
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
        public abstract ValueTask<bool> AddMemberAsync(ClusterMemberId id, TAddress address, CancellationToken token = default);

        /// <summary>
        /// Proposes removal of the existing member.
        /// </summary>
        /// <param name="id">The identifier of the cluster member to remove.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>
        /// <see langword="true"/> if the new member is added to the proposed configuration;
        /// <see langword="false"/> if the storage has the proposed configuration already.
        /// </returns>
        public abstract ValueTask<bool> RemoveMemberAsync(ClusterMemberId id, CancellationToken token = default);

        /// <inheritdoc />
        IAsyncEnumerable<ClusterConfigurationEvent<TAddress>> IClusterConfigurationStorage<TAddress>.PollChangesAsync(CancellationToken token)
            => events.Reader.ReadAllAsync(token);

        private protected async ValueTask CompareAsync(IReadOnlyDictionary<ClusterMemberId, TAddress> active, IReadOnlyDictionary<ClusterMemberId, TAddress> proposed)
        {
            foreach (var (id, address) in active)
            {
                if (!proposed.ContainsKey(id))
                    await events.Writer.WriteAsync(new() { Id = id, Address = address, IsAdded = false }).ConfigureAwait(false);
            }

            foreach (var (id, address) in proposed)
            {
                if (!active.ContainsKey(id))
                    await events.Writer.WriteAsync(new() { Id = id, Address = address, IsAdded = true }).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                activeCache = activeCache.Clear();
                proposedCache = proposedCache.Clear();
                events.Writer.TryComplete();
            }

            base.Dispose(disposing);
        }
    }
}
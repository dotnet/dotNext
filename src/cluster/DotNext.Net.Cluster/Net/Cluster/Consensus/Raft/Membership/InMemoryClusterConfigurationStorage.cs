using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using static System.Threading.Timeout;

namespace DotNext.Net.Cluster.Consensus.Raft.Membership
{
    using Buffers;
    using IO;
    using static Threading.Tasks.Continuation;

    /// <summary>
    /// Represents in-memory storage of cluster configuration.
    /// </summary>
    /// <typeparam name="TAddress">The type of cluster member address.</typeparam>
    public abstract class InMemoryClusterConfigurationStorage<TAddress> : Disposable, IClusterConfigurationStorage<TAddress>
        where TAddress : notnull
    {
        private const int InitialBufferSize = 512;

        private sealed class ClusterConfiguration : Disposable, IClusterConfiguration
        {
            private MemoryOwner<byte> payload;

            internal ClusterConfiguration(long fingerprint, MemoryOwner<byte> configuration)
            {
                payload = configuration;
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
                storage.active = new(storage.fingerprintSource.Next<long>(), config);
            }
        }

        private readonly MemoryAllocator<byte>? allocator;
        private readonly Random fingerprintSource;
        private ImmutableDictionary<ClusterMemberId, TAddress> activeCache, proposedCache;
        private readonly Channel<ClusterConfigurationEvent<TAddress>> events;
        private volatile TaskCompletionSource<bool> activatedEvent;
        private ClusterConfiguration? active, proposed;

        /// <summary>
        /// Initializes a new in-memory configuration storage.
        /// </summary>
        /// <param name="allocator">The memory allocator.</param>
        protected InMemoryClusterConfigurationStorage(MemoryAllocator<byte>? allocator = null)
        {
            this.allocator = allocator;
            fingerprintSource = new();
            activeCache = ImmutableDictionary<ClusterMemberId, TAddress>.Empty;
            proposedCache = ImmutableDictionary<ClusterMemberId, TAddress>.Empty;
            activatedEvent = new(TaskCreationOptions.RunContinuationsAsynchronously);
            events = Channel.CreateBounded<ClusterConfigurationEvent<TAddress>>(new BoundedChannelOptions(10) { FullMode = BoundedChannelFullMode.Wait });
        }

        private void OnActivated() => Interlocked.Exchange(ref activatedEvent, new(TaskCreationOptions.RunContinuationsAsynchronously)).SetResult(true);

        /// <summary>
        /// Encodes the address to its binary representation.
        /// </summary>
        /// <param name="address">The address to encode.</param>
        /// <param name="output">The buffer for the address.</param>
        protected abstract void Encode(TAddress address, ref BufferWriterSlim<byte> output);

        private void Encode(IReadOnlyDictionary<ClusterMemberId, TAddress> configuration, ref BufferWriterSlim<byte> output)
        {
            output.WriteInt32(configuration.Count, true);

            foreach (var (id, address) in configuration)
            {
                // serialize id
                id.WriteTo(output.GetSpan(ClusterMemberId.Size));
                output.Advance(ClusterMemberId.Size);

                // serialize address
                Encode(address, ref output);
            }
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
        /// Decodes the address of the node from its binary representation.
        /// </summary>
        /// <param name="reader">The reader of binary data.</param>
        /// <returns>The decoded address.</returns>
        protected abstract TAddress Decode(ref SequenceBinaryReader reader);

        private void Decode(IDictionary<ClusterMemberId, TAddress> output, ref SequenceBinaryReader reader)
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

        private void Decode(IDictionary<ClusterMemberId, TAddress> output, ReadOnlyMemory<byte> memory)
        {
            var reader = IAsyncBinaryReader.Create(memory);
            Decode(output, ref reader);
        }

        /// <summary>
        /// Gets active configuration.
        /// </summary>
        public IClusterConfiguration ActiveConfiguration
            => active ??= new(fingerprintSource.Next<long>(), Encode(activeCache));

        /// <summary>
        /// Gets proposed configuration.
        /// </summary>
        public IClusterConfiguration? ProposedConfiguration => proposed;

        /// <summary>
        /// Proposes the configuration.
        /// </summary>
        /// <param name="configuration">The proposed configuration.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result.</returns>
        public async ValueTask ProposeAsync(IClusterConfiguration configuration, CancellationToken token)
        {
            var config = await configuration.ToMemoryAsync(allocator, token).ConfigureAwait(false);

            proposed?.Dispose();
            proposed = new(configuration.Fingerprint, config);

            proposedCache.Clear();
            Decode(proposedCache, config.Memory);
        }

        /// <summary>
        /// Applies proposed configuration as active configuration.
        /// </summary>
        public void Apply()
        {
            active?.Dispose();
            active = proposed;
            activeCache = proposedCache;

            proposed?.Dispose();
            proposed = null;
            proposedCache = ImmutableDictionary<ClusterMemberId, TAddress>.Empty;

            OnActivated();
        }

        /// <inheritdoc />
        ValueTask IClusterConfigurationStorage.ApplyAsync(CancellationToken token)
        {
            ValueTask result;
            if (token.IsCancellationRequested)
            {
                result = ValueTask.FromCanceled(token);
            }
            else
            {
                result = new();
                try
                {
                    Apply();
                }
                catch (Exception e)
                {
                    result = ValueTask.FromException(e);
                }
            }

            return result;
        }

        /// <inheritdoc />
        Task IClusterConfigurationStorage.WaitForApplyAsync(CancellationToken token)
            => proposed is null ? Task.CompletedTask : activatedEvent.Task.ContinueWithTimeout(InfiniteTimeSpan, token);

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
        public async ValueTask<bool> AddMemberAsync(ClusterMemberId id, TAddress address, CancellationToken token = default)
        {
            if (proposed is not null || activeCache.ContainsKey(id))
                return false;

            var builder = activeCache.ToBuilder();
            builder.Add(id, address);
            activeCache = builder.ToImmutable();

            active?.Dispose();
            active = new(fingerprintSource.Next<long>(), Encode(builder));
            builder.Clear();

            await events.Writer.WriteAsync(new() { Id = id, Address = address, IsAdded = true }, token).ConfigureAwait(false);
            return true;
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
        public async ValueTask<bool> RemoveMemberAsync(ClusterMemberId id, CancellationToken token = default)
        {
            if (proposed is not null || !activeCache.ContainsKey(id))
                return false;

            var builder = activeCache.ToBuilder();
            if (!builder.Remove(id, out var address))
                return false;
            activeCache = builder.ToImmutable();

            active?.Dispose();
            active = new(fingerprintSource.Next<long>(), Encode(builder));
            builder.Clear();

            await events.Writer.WriteAsync(new() { Id = id, Address = address, IsAdded = false }, token).ConfigureAwait(false);
            return true;
        }

        /// <inheritdoc />
        IAsyncEnumerable<ClusterConfigurationEvent<TAddress>> IClusterConfigurationStorage<TAddress>.PollChangesAsync(CancellationToken token)
            => events.Reader.ReadAllAsync(token);

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
                activeCache.Clear();
                active = null;

                proposed?.Dispose();
                proposedCache.Clear();
                proposed = null;

                events.Writer.TryComplete();
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

        protected override IPEndPoint Decode(ref SequenceBinaryReader reader)
            => (IPEndPoint)reader.ReadEndPoint();
    }
}
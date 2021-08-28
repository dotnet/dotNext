using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Buffers;
    using IO;
    using IO.Log;
    using Membership;
    using TransportServices;
    using IClientMetricsCollector = Metrics.IClientMetricsCollector;

    /// <summary>
    /// Represents default implementation of Raft-based cluster.
    /// </summary>
    public partial class RaftCluster : RaftCluster<RaftClusterMember>, ILocalMember
    {
        [StructLayout(LayoutKind.Auto)]
        private struct ClusterConfiguration : IClusterConfiguration, IDisposable
        {
            private MemoryOwner<byte> configuration;

            internal ClusterConfiguration(MemoryOwner<byte> content, long fingerprint, bool applyConfig)
            {
                configuration = content;
                IsApplied = applyConfig;
                Fingerprint = fingerprint;
            }

            public readonly long Fingerprint { get; }

            internal readonly bool IsApplied { get; }

            readonly long IClusterConfiguration.Length => configuration.Length;

            readonly bool IDataTransferObject.IsReusable => true;

            readonly ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
                => writer.WriteAsync(configuration.Memory, null, token);

            readonly ValueTask<TResult> IDataTransferObject.TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
                => transformation.TransformAsync<SequenceBinaryReader>(IAsyncBinaryReader.Create(configuration.Memory), token);

            readonly bool IDataTransferObject.TryGetMemory(out ReadOnlyMemory<byte> memory)
            {
                memory = configuration.Memory;
                return true;
            }

            public void Dispose()
            {
                configuration.Dispose();
                this = default;
            }
        }

        private readonly ImmutableDictionary<string, string> metadata;
        private readonly Func<ILocalMember, IPEndPoint, ClusterMemberId, IClientMetricsCollector?, RaftClusterMember> clientFactory;
        private readonly Func<ILocalMember, IServer> serverFactory;
        private readonly IPEndPoint publicEndPoint;
        private readonly MemoryAllocator<byte>? allocator;
        private readonly ClusterMemberAnnouncer<IPEndPoint>? announcer;
        private readonly int warmupRounds;
        private readonly bool coldStart;
        private Task pollingLoopTask;
        private IServer? server;
        private ClusterConfiguration cachedConfig;

        /// <summary>
        /// Initializes a new default implementation of Raft-based cluster.
        /// </summary>
        /// <param name="configuration">The configuration of the cluster.</param>
        public RaftCluster(NodeConfiguration configuration)
            : base(configuration)
        {
            Metrics = configuration.Metrics;
            metadata = ImmutableDictionary.CreateRange(StringComparer.Ordinal, configuration.Metadata);
            clientFactory = configuration.CreateMemberClient;
            serverFactory = configuration.CreateServer;
            publicEndPoint = configuration.PublicEndPoint;
            allocator = configuration.MemoryAllocator;
            announcer = configuration.Announcer;
            warmupRounds = configuration.WarmupRounds;
            coldStart = configuration.ColdStart;
            ConfigurationStorage = configuration.ConfigurationStorage ?? new InMemoryClusterConfigurationStorage(allocator);
            pollingLoopTask = Task.CompletedTask;
        }

        /// <inheritdoc />
        protected sealed override IClusterConfigurationStorage<IPEndPoint> ConfigurationStorage { get; }

        /// <summary>
        /// Starts serving local member.
        /// </summary>
        /// <param name="token">The token that can be used to cancel initialization process.</param>
        /// <returns>The task representing asynchronous execution of the method.</returns>
        public override async Task StartAsync(CancellationToken token = default)
        {
            if (coldStart)
            {
                // in case of cold start, add the local member to the configuration
                await ConfigurationStorage.AddMemberAsync(LocalMemberId, publicEndPoint, token).ConfigureAwait(false);
                await ConfigurationStorage.ApplyAsync(token).ConfigureAwait(false);
            }

            pollingLoopTask = ConfigurationPollingLoop();
            server = serverFactory(this);
            server.Start();
            await base.StartAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Stops serving local member.
        /// </summary>
        /// <param name="token">The token that can be used to cancel shutdown process.</param>
        /// <returns>The task representing asynchronous execution of the method.</returns>
        public override Task StopAsync(CancellationToken token = default)
        {
            server?.Dispose();
            server = null;
            return base.StopAsync(token);
        }

        private async Task ConfigurationPollingLoop()
        {
            await foreach (var eventInfo in ConfigurationStorage.PollChangesAsync(LifecycleToken))
            {
                if (eventInfo.IsAdded)
                {
                    var member = clientFactory.Invoke(this, eventInfo.Address, eventInfo.Id, Metrics as IClientMetricsCollector);
                    if (await AddMemberAsync(member, LifecycleToken).ConfigureAwait(false))
                        member.IsRemote = !Equals(eventInfo.Address, publicEndPoint);
                    else
                        member.Dispose();
                }
                else
                {
                    var member = await RemoveMember(eventInfo.Id, LifecycleToken).ConfigureAwait(false);
                    member?.Dispose();
                }
            }
        }

        /// <inheritdoc />
        bool ILocalMember.IsLeader(IRaftClusterMember member) => ReferenceEquals(Leader, member);

        /// <inheritdoc />
        ref readonly ClusterMemberId ILocalMember.Id => ref LocalMemberId;

        /// <inheritdoc />
        IReadOnlyDictionary<string, string> ILocalMember.Metadata => metadata;

        /// <inheritdoc />
        Task<bool> ILocalMember.ResignAsync(CancellationToken token)
            => ResignAsync(token);

        async Task ILocalMember.InstallConfigurationAsync<TConfiguration>(TConfiguration configuration, bool applyConfig, CancellationToken token)
        {
            var buffer = await configuration.ToMemoryAsync(allocator, token).ConfigureAwait(false);
            cachedConfig.Dispose();
            cachedConfig = new(buffer, configuration.Fingerprint, applyConfig);
        }

        /// <inheritdoc />
        async Task<Result<bool>> ILocalMember.AppendEntriesAsync<TEntry>(ClusterMemberId sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
        {
            TryGetMember(sender)?.Touch();
            Result<bool> result;

            try
            {
                result = await AppendEntriesAsync(sender, senderTerm, entries, prevLogIndex, prevLogTerm, commitIndex, cachedConfig, cachedConfig.IsApplied, token).ConfigureAwait(false);
            }
            finally
            {
                cachedConfig.Dispose();
            }

            return result;
        }

        /// <inheritdoc />
        Task<Result<bool>> ILocalMember.VoteAsync(ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
        {
            var member = TryGetMember(sender);
            if (member is null)
                return Task.FromResult(new Result<bool>(Term, false));

            member.Touch();
            return VoteAsync(sender, term, lastLogIndex, lastLogTerm, token);
        }

        /// <inheritdoc />
        Task<Result<bool>> ILocalMember.PreVoteAsync(ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
        {
            TryGetMember(sender)?.Touch();
            return PreVoteAsync(term + 1L, lastLogIndex, lastLogTerm, token);
        }

        /// <inheritdoc />
        Task<Result<bool>> ILocalMember.InstallSnapshotAsync<TSnapshot>(ClusterMemberId sender, long senderTerm, TSnapshot snapshot, long snapshotIndex, CancellationToken token)
        {
            TryGetMember(sender)?.Touch();

            return InstallSnapshotAsync(sender, senderTerm, snapshot, snapshotIndex, token);
        }

        private void Cleanup()
        {
            server?.Dispose();
            server = null;

            cachedConfig.Dispose();
        }

        /// <summary>
        /// Releases managed and unmanaged resources associated with this object.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> if called from <see cref="Disposable.Dispose()"/>; <see langword="false"/> if called from finalizer <see cref="Disposable.Finalize()"/>.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Cleanup();

            base.Dispose(disposing);
        }

        /// <inheritdoc />
        protected override async ValueTask DisposeAsyncCore()
        {
            await base.DisposeAsyncCore().ConfigureAwait(false);
            Cleanup();
        }
    }
}
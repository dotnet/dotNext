using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO.Log;
    using TransportServices;
    using IClientMetricsCollector = Metrics.IClientMetricsCollector;

    /// <summary>
    /// Represents default implementation of Raft-based cluster.
    /// </summary>
    public partial class RaftCluster : RaftCluster<RaftClusterMember>, ILocalMember
    {
        private readonly ImmutableDictionary<string, string> metadata;
        private readonly ClusterMemberId localMemberId;
        private readonly Func<ILocalMember, IPEndPoint, IClientMetricsCollector?, RaftClusterMember> clientFactory;
        private readonly Func<ILocalMember, IServer> serverFactory;
        private readonly RaftLogEntriesBufferingOptions? bufferingOptions;
        private IServer? server;

        /// <summary>
        /// Initializes a new default implementation of Raft-based cluster.
        /// </summary>
        /// <param name="configuration">The configuration of the cluster.</param>
        public RaftCluster(NodeConfiguration configuration)
            : base(configuration, out var members)
        {
            Metrics = configuration.Metrics;
            localMemberId = ClusterMemberId.FromEndPoint(configuration.PublicEndPoint);
            metadata = ImmutableDictionary.CreateRange(StringComparer.Ordinal, configuration.Metadata);
            clientFactory = configuration.CreateMemberClient;
            serverFactory = configuration.CreateServer;
            bufferingOptions = configuration.BufferingOptions;

            // create members without starting clients
            foreach (var member in configuration.Members)
                members.Add(configuration.CreateMemberClient(this, member, configuration.Metrics as IClientMetricsCollector));
        }

        /// <inheritdoc />
        protected sealed override ClusterMemberId? LocalMember => localMemberId;

        /// <summary>
        /// Starts serving local member.
        /// </summary>
        /// <param name="token">The token that can be used to cancel initialization process.</param>
        /// <returns>The task representing asynchronous execution of the method.</returns>
        public override Task StartAsync(CancellationToken token = default)
        {
            if (TryGetMember(localMemberId) is null)
                throw new RaftProtocolException(ExceptionMessages.UnresolvedLocalMember);

            server = serverFactory(this);
            server.Start();
            return base.StartAsync(token);
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

        /// <summary>
        /// Initializes a new client for communication with cluster member.
        /// </summary>
        /// <remarks>
        /// This method is needed if you want to implement dynamic addition of the new cluster members.
        /// </remarks>
        /// <param name="address">The address of the cluster member.</param>
        /// <returns>A new client for communication with cluster member.</returns>
        protected RaftClusterMember CreateClient(IPEndPoint address)
            => clientFactory(this, address, Metrics as IClientMetricsCollector);

        /// <inheritdoc />
        bool ILocalMember.IsLeader(IRaftClusterMember member) => ReferenceEquals(Leader, member);

        /// <inheritdoc />
        ref readonly ClusterMemberId ILocalMember.Id => ref localMemberId;

        /// <inheritdoc />
        IReadOnlyDictionary<string, string> ILocalMember.Metadata => metadata;

        /// <inheritdoc />
        Task<bool> ILocalMember.ResignAsync(CancellationToken token)
            => ResignAsync(token);

        private async Task<Result<bool>> BufferizeReceivedEntriesAsync<TEntry>(ClusterMemberId sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry
        {
            Debug.Assert(bufferingOptions is not null);
            using var buffered = await BufferedRaftLogEntryList.CopyAsync(entries, bufferingOptions, token).ConfigureAwait(false);
            return await AppendEntriesAsync(sender, senderTerm, buffered.ToProducer(), prevLogIndex, prevLogTerm, commitIndex, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        Task<Result<bool>> ILocalMember.AppendEntriesAsync<TEntry>(ClusterMemberId sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
        {
            TryGetMember(sender)?.Touch();

            return bufferingOptions is null ?
                AppendEntriesAsync(sender, senderTerm, entries, prevLogIndex, prevLogTerm, commitIndex, token) :
                BufferizeReceivedEntriesAsync(sender, senderTerm, entries, prevLogIndex, prevLogTerm, commitIndex, token);
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

        private async Task<Result<bool>> BufferizeSnapshotAsync<TSnapshot>(ClusterMemberId sender, long senderTerm, TSnapshot snapshot, long snapshotIndex, CancellationToken token)
            where TSnapshot : notnull, IRaftLogEntry
        {
            Debug.Assert(bufferingOptions is not null);
            using var buffered = await BufferedRaftLogEntry.CopyAsync(snapshot, bufferingOptions, token).ConfigureAwait(false);
            return await InstallSnapshotAsync(sender, senderTerm, buffered, snapshotIndex, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        Task<Result<bool>> ILocalMember.InstallSnapshotAsync<TSnapshot>(ClusterMemberId sender, long senderTerm, TSnapshot snapshot, long snapshotIndex, CancellationToken token)
        {
            TryGetMember(sender)?.Touch();

            return bufferingOptions is null ?
                InstallSnapshotAsync(sender, senderTerm, snapshot, snapshotIndex, token) :
                BufferizeSnapshotAsync(sender, senderTerm, snapshot, snapshotIndex, token);
        }

        private void Cleanup()
        {
            server?.Dispose();
            server = null;
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
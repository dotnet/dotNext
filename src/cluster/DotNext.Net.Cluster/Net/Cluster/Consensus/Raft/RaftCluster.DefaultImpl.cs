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
        private static readonly Func<RaftClusterMember, EndPoint, bool> MatchByEndPoint = IsMatchedByEndPoint;

        private readonly ImmutableDictionary<string, string> metadata;
        private readonly IPEndPoint publicEndPoint;
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
            publicEndPoint = configuration.PublicEndPoint;
            metadata = ImmutableDictionary.CreateRange(StringComparer.Ordinal, configuration.Metadata);
            clientFactory = configuration.CreateMemberClient;
            serverFactory = configuration.CreateServer;
            bufferingOptions = configuration.BufferingOptions;

            // create members without starting clients
            foreach (var member in configuration.Members)
                members.Add(configuration.CreateMemberClient(this, member, configuration.Metrics as IClientMetricsCollector));
        }

        private static bool IsMatchedByEndPoint(RaftClusterMember member, EndPoint endPoint)
            => member.EndPoint.Equals(endPoint);

        /// <summary>
        /// Starts serving local member.
        /// </summary>
        /// <param name="token">The token that can be used to cancel initialization process.</param>
        /// <returns>The task representing asynchronous execution of the method.</returns>
        public override Task StartAsync(CancellationToken token = default)
        {
            if (FindMember(MatchByEndPoint, publicEndPoint) is null)
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
        IPEndPoint ILocalMember.Address => publicEndPoint;

        /// <inheritdoc />
        IReadOnlyDictionary<string, string> ILocalMember.Metadata => metadata;

        /// <inheritdoc />
        Task<bool> ILocalMember.ResignAsync(CancellationToken token)
            => ReceiveResignAsync(token);

        private async Task<Result<bool>> BufferizeReceivedEntriesAsync<TEntry>(RaftClusterMember sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry
        {
            Debug.Assert(bufferingOptions is not null);
            using var buffered = await BufferedRaftLogEntryList.CopyAsync(entries, bufferingOptions, token).ConfigureAwait(false);
            return await ReceiveEntriesAsync(sender, senderTerm, buffered.ToProducer(), prevLogIndex, prevLogTerm, commitIndex, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        Task<Result<bool>> ILocalMember.ReceiveEntriesAsync<TEntry>(EndPoint sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
        {
            var member = FindMember(MatchByEndPoint, sender);
            if (member is null)
                return Task.FromResult(new Result<bool>(Term, false));

            member.Touch();
            return bufferingOptions is null ?
                ReceiveEntriesAsync(member, senderTerm, entries, prevLogIndex, prevLogTerm, commitIndex, token) :
                BufferizeReceivedEntriesAsync(member, senderTerm, entries, prevLogIndex, prevLogTerm, commitIndex, token);
        }

        /// <inheritdoc />
        Task<Result<bool>> ILocalMember.ReceiveVoteAsync(EndPoint sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
        {
            var member = FindMember(MatchByEndPoint, sender);
            if (member is null)
                return Task.FromResult(new Result<bool>(Term, false));

            member.Touch();
            return ReceiveVoteAsync(member, term, lastLogIndex, lastLogTerm, token);
        }

        /// <inheritdoc />
        Task<Result<bool>> ILocalMember.ReceivePreVoteAsync(EndPoint sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
        {
            var member = FindMember(MatchByEndPoint, sender);
            if (member is null)
                return Task.FromResult(new Result<bool>(Term, false));

            member.Touch();
            return ReceivePreVoteAsync(term + 1L, lastLogIndex, lastLogTerm, token);
        }

        private async Task<Result<bool>> BufferizeSnapshotAsync<TSnapshot>(RaftClusterMember sender, long senderTerm, TSnapshot snapshot, long snapshotIndex, CancellationToken token)
            where TSnapshot : notnull, IRaftLogEntry
        {
            Debug.Assert(bufferingOptions is not null);
            using var buffered = await BufferedRaftLogEntry.CopyAsync(snapshot, bufferingOptions, token).ConfigureAwait(false);
            return await ReceiveSnapshotAsync(sender, senderTerm, buffered, snapshotIndex, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        Task<Result<bool>> ILocalMember.ReceiveSnapshotAsync<TSnapshot>(EndPoint sender, long senderTerm, TSnapshot snapshot, long snapshotIndex, CancellationToken token)
        {
            var member = FindMember(MatchByEndPoint, sender);
            if (member is null)
                return Task.FromResult(new Result<bool>(Term, false));

            member.Touch();
            return bufferingOptions is null ?
                ReceiveSnapshotAsync(member, senderTerm, snapshot, snapshotIndex, token) :
                BufferizeSnapshotAsync(member, senderTerm, snapshot, snapshotIndex, token);
        }

        /// <summary>
        /// Releases managed and unmanaged resources associated with this object.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> if called from <see cref="Disposable.Dispose()"/>; <see langword="false"/> if called from finalizer <see cref="Disposable.Finalize()"/>.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                server?.Dispose();
                server = null;
            }

            base.Dispose(disposing);
        }
    }
}
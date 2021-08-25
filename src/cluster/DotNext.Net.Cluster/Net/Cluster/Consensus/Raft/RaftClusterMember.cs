using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Threading;
    using TransportServices;
    using IClientMetricsCollector = Metrics.IClientMetricsCollector;

    /// <summary>
    /// Represents Raft cluster member that is accessible through the network.
    /// </summary>
    public abstract class RaftClusterMember : Disposable, IRaftClusterMember
    {
        private long nextIndex;
        private protected readonly IClientMetricsCollector? metrics;
        private protected readonly ILocalMember localMember;
        internal readonly ClusterMemberId Id;
        private volatile IReadOnlyDictionary<string, string>? metadataCache;
        private AtomicEnum<ClusterMemberStatus> status;

        private protected RaftClusterMember(ILocalMember localMember, EndPoint endPoint, bool isRemote, ClusterMemberId? id, IClientMetricsCollector? metrics)
        {
            this.localMember = localMember;
            this.metrics = metrics;
            EndPoint = endPoint;
            status = new AtomicEnum<ClusterMemberStatus>(ClusterMemberStatus.Unknown);
            Id = id ?? ClusterMemberId.FromEndPoint(endPoint);
            IsRemote = isRemote;
        }

        /// <inheritdoc />
        ClusterMemberId IClusterMember.Id => Id;

        private protected ILogger Logger => localMember.Logger;

        /// <summary>
        /// Gets the address of this cluster member.
        /// </summary>
        public EndPoint EndPoint { get; }

        /// <summary>
        /// Determines whether this member is a leader.
        /// </summary>
        public bool IsLeader => localMember.IsLeader(this);

        /// <summary>
        /// Determines whether this member is not a local node.
        /// </summary>
        public bool IsRemote { get; }

        /// <summary>
        /// Gets the status of this member.
        /// </summary>
        public ClusterMemberStatus Status => status.Value;

        /// <summary>
        /// Informs about status change.
        /// </summary>
        public event ClusterMemberStatusChanged? MemberStatusChanged;

        /// <inheritdoc/>
        ref long IRaftClusterMember.NextIndex => ref nextIndex;

        /// <summary>
        /// Cancels pending requests scheduled for this member.
        /// </summary>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        public abstract ValueTask CancelPendingRequestsAsync();

        private protected void ChangeStatus(ClusterMemberStatus newState)
            => IClusterMember.OnMemberStatusChanged(this, ref status, newState, MemberStatusChanged);

        internal void Touch() => ChangeStatus(ClusterMemberStatus.Available);

        private protected abstract Task<Result<bool>> VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token);

        /// <inheritdoc/>
        Task<Result<bool>> IRaftClusterMember.VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
            => IsRemote ? VoteAsync(term, lastLogIndex, lastLogTerm, token) : Task.FromResult(new Result<bool>(term, true));

        private protected abstract Task<Result<bool>> PreVoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token);

        /// <inheritdoc/>
        Task<Result<bool>> IRaftClusterMember.PreVoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
            => IsRemote ? PreVoteAsync(term, lastLogIndex, lastLogTerm, token) : Task.FromResult(new Result<bool>(term, true));

        private protected abstract Task<Result<bool>> AppendEntriesAsync<TEntry, TList>(long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
            where TEntry : IRaftLogEntry
            where TList : IReadOnlyList<TEntry>;

        /// <inheritdoc/>
        Task<Result<bool>> IRaftClusterMember.AppendEntriesAsync<TEntry, TList>(long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
            => IsRemote ? AppendEntriesAsync<TEntry, TList>(term, entries, prevLogIndex, prevLogTerm, commitIndex, token) : Task.FromResult(new Result<bool>(term, true));

        private protected abstract Task<Result<bool>> InstallSnapshotAsync(long term, IRaftLogEntry snapshot, long snapshotIndex, CancellationToken token);

        /// <inheritdoc/>
        Task<Result<bool>> IRaftClusterMember.InstallSnapshotAsync(long term, IRaftLogEntry snapshot, long snapshotIndex, CancellationToken token)
            => IsRemote ? InstallSnapshotAsync(term, snapshot, snapshotIndex, token) : Task.FromResult(new Result<bool>(term, true));

        private protected abstract Task<bool> ResignAsync(CancellationToken token);

        /// <inheritdoc/>
        Task<bool> IClusterMember.ResignAsync(CancellationToken token) => ResignAsync(token);

        private protected abstract Task<IReadOnlyDictionary<string, string>> GetMetadataAsync(CancellationToken token);

        /// <inheritdoc/>
        async ValueTask<IReadOnlyDictionary<string, string>> IClusterMember.GetMetadataAsync(bool refresh, CancellationToken token)
        {
            if (!IsRemote)
                return localMember.Metadata;

            if (metadataCache is null || refresh)
                metadataCache = await GetMetadataAsync(token).ConfigureAwait(false);

            return metadataCache;
        }
    }
}
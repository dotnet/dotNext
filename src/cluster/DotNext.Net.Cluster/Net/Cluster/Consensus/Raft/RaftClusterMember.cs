using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

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
        private volatile IReadOnlyDictionary<string, string>? metadataCache;
        private AtomicEnum<ClusterMemberStatus> status;
        private readonly ILocalMember localMember;

        private protected RaftClusterMember(ILocalMember localMember, IPEndPoint endPoint, IClientMetricsCollector? metrics)
        {
            this.localMember = localMember;
            this.metrics = metrics;
            Endpoint = endPoint;
            status = new AtomicEnum<ClusterMemberStatus>(ClusterMemberStatus.Unknown);
        }

        private protected int LocalPort => localMember.Address.Port;

        private protected ILogger Logger => localMember.Logger;

        /// <summary>
        /// Gets the address of this cluster member.
        /// </summary>
        /// <value></value>
        public IPEndPoint Endpoint { get; }

        /// <summary>
        /// Determines whether this member is a leader.
        /// </summary>
        public bool IsLeader => localMember.IsLeader(this);

        /// <summary>
        /// Determines whether this member is not a local node.
        /// </summary>
        public bool IsRemote => !Endpoint.Equals(localMember.Address);

        /// <summary>
        /// Gets the status of this member.
        /// </summary>
        public ClusterMemberStatus Status => status.Value;

        /// <summary>
        /// Informs about status change.
        /// </summary>
        public event ClusterMemberStatusChanged? MemberStatusChanged;

        ref long IRaftClusterMember.NextIndex => ref nextIndex;

        /// <summary>
        /// Cancels pending requests scheduled for this member.
        /// </summary>
        public abstract void CancelPendingRequests();

        private protected void ChangeStatus(ClusterMemberStatus newState)
            => IClusterMember.OnMemberStatusChanged(this, ref status, newState, MemberStatusChanged);

        private protected abstract Task<Result<bool>> VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token);

        Task<Result<bool>> IRaftClusterMember.VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
            => Endpoint.Equals(localMember.Address) ?
                Task.FromResult(new Result<bool>(term, true)) :
                VoteAsync(term, lastLogIndex, lastLogTerm, token);

        private protected abstract Task<Result<bool>> AppendEntriesAsync<TEntry, TList>(long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
            where TEntry : IRaftLogEntry
            where TList : IReadOnlyList<TEntry>;

        Task<Result<bool>> IRaftClusterMember.AppendEntriesAsync<TEntry, TList>(long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
        {
            if (Endpoint.Equals(localMember.Address))
                return Task.FromResult(new Result<bool>(term, true));
            return AppendEntriesAsync<TEntry, TList>(term, entries, prevLogIndex, prevLogTerm, commitIndex, token);
        }

        private protected abstract Task<Result<bool>> InstallSnapshotAsync(long term, IRaftLogEntry snapshot, long snapshotIndex, CancellationToken token);

        Task<Result<bool>> IRaftClusterMember.InstallSnapshotAsync(long term, IRaftLogEntry snapshot, long snapshotIndex, CancellationToken token)
        {
            if (Endpoint.Equals(localMember.Address))
                return Task.FromResult(new Result<bool>(term, true));
            return InstallSnapshotAsync(term, snapshot, snapshotIndex, token);
        }

        private protected abstract Task<bool> ResignAsync(CancellationToken token);

        Task<bool> IClusterMember.ResignAsync(CancellationToken token) => ResignAsync(token);

        private protected abstract Task<IReadOnlyDictionary<string, string>> GetMetadataAsync(CancellationToken token);

        async ValueTask<IReadOnlyDictionary<string, string>> IClusterMember.GetMetadataAsync(bool refresh, CancellationToken token)
        {
            if (Endpoint.Equals(localMember.Address))
                return localMember.Metadata;
            if (metadataCache is null || refresh)
                metadataCache = await GetMetadataAsync(token).ConfigureAwait(false);
            return metadataCache;
        }
    }
}
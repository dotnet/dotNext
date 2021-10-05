using System.Net;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Collections.Specialized;
using Membership;
using Threading;
using TransportServices;
using IClientMetricsCollector = Metrics.IClientMetricsCollector;

/// <summary>
/// Represents Raft cluster member that is accessible through the network.
/// </summary>
public abstract class RaftClusterMember : Disposable, IRaftClusterMember
{
    private long nextIndex, fingerprint;
    private protected readonly IClientMetricsCollector? metrics;
    private protected readonly ILocalMember localMember;
    internal readonly ClusterMemberId Id;
    private volatile IReadOnlyDictionary<string, string>? metadataCache;
    private AtomicEnum<ClusterMemberStatus> status;
    private InvocationList<Action<ClusterMemberStatusChangedEventArgs<RaftClusterMember>>> statusChangedHandlers;

    private protected RaftClusterMember(ILocalMember localMember, IPEndPoint endPoint, ClusterMemberId id, IClientMetricsCollector? metrics)
    {
        this.localMember = localMember;
        this.metrics = metrics;
        EndPoint = endPoint;
        status = new AtomicEnum<ClusterMemberStatus>(ClusterMemberStatus.Unknown);
        Id = id;
    }

    /// <inheritdoc />
    ClusterMemberId IClusterMember.Id => Id;

    private protected ILogger Logger => localMember.Logger;

    /// <summary>
    /// Gets the address of this cluster member.
    /// </summary>
    public IPEndPoint EndPoint { get; }

    /// <inheritdoc />
    EndPoint IPeer.EndPoint => EndPoint;

    /// <summary>
    /// Determines whether this member is a leader.
    /// </summary>
    public bool IsLeader => localMember.IsLeader(this);

    /// <summary>
    /// Determines whether this member is not a local node.
    /// </summary>
    public bool IsRemote { get; internal set; }

    /// <summary>
    /// Gets the status of this member.
    /// </summary>
    public ClusterMemberStatus Status => status.Value;

    /// <summary>
    /// Informs about status change.
    /// </summary>
    public event Action<ClusterMemberStatusChangedEventArgs<RaftClusterMember>> MemberStatusChanged
    {
        add => statusChangedHandlers += value;
        remove => statusChangedHandlers -= value;
    }

    /// <inheritdoc />
    event Action<ClusterMemberStatusChangedEventArgs> IClusterMember.MemberStatusChanged
    {
        add => statusChangedHandlers += value;
        remove => statusChangedHandlers -= value;
    }

    /// <inheritdoc/>
    ref long IRaftClusterMember.NextIndex => ref nextIndex;

    /// <inheritdoc/>
    ref long IRaftClusterMember.ConfigurationFingerprint => ref fingerprint;

    /// <summary>
    /// Cancels pending requests scheduled for this member.
    /// </summary>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    public abstract ValueTask CancelPendingRequestsAsync();

    private protected void ChangeStatus(ClusterMemberStatus newState)
        => IClusterMember.OnMemberStatusChanged(this, ref status, newState, ref statusChangedHandlers);

    internal void Touch() => ChangeStatus(ClusterMemberStatus.Available);

    private protected abstract Task<Result<bool>> VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token);

    /// <inheritdoc/>
    Task<Result<bool>> IRaftClusterMember.VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
        => IsRemote ? VoteAsync(term, lastLogIndex, lastLogTerm, token) : Task.FromResult(new Result<bool>(term, true));

    private protected abstract Task<Result<bool>> PreVoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token);

    /// <inheritdoc/>
    Task<Result<bool>> IRaftClusterMember.PreVoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
        => IsRemote ? PreVoteAsync(term, lastLogIndex, lastLogTerm, token) : Task.FromResult(new Result<bool>(term, true));

    private protected abstract Task<Result<bool>> AppendEntriesAsync<TEntry, TList>(long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, IClusterConfiguration config, bool applyConfig, CancellationToken token)
        where TEntry : IRaftLogEntry
        where TList : IReadOnlyList<TEntry>;

    /// <inheritdoc/>
    Task<Result<bool>> IRaftClusterMember.AppendEntriesAsync<TEntry, TList>(long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, IClusterConfiguration config, bool applyConfig, CancellationToken token)
        => IsRemote ? AppendEntriesAsync<TEntry, TList>(term, entries, prevLogIndex, prevLogTerm, commitIndex, config, applyConfig, token) : Task.FromResult(new Result<bool>(term, true));

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
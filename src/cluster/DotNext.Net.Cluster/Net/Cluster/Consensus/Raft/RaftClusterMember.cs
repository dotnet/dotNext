using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Collections.Specialized;
using Membership;
using TransportServices;

/// <summary>
/// Represents Raft cluster member that is accessible through the network.
/// </summary>
public abstract class RaftClusterMember : Disposable, IRaftClusterMember
{
    private protected static readonly Histogram<double> ResponseTimeMeter = Raft.Metrics.Instrumentation.ClientSide.CreateHistogram<double>("response-time", unit: "ms", description: "Response Time");

    private protected readonly ILocalMember localMember;
    private readonly TimeSpan requestTimeout;
    internal readonly ClusterMemberId Id;
    private protected readonly KeyValuePair<string, object?> cachedRemoteAddressAttribute;
    private volatile IReadOnlyDictionary<string, string>? metadataCache;
    private volatile ClusterMemberStatus status;
    private InvocationList<Action<ClusterMemberStatusChangedEventArgs<RaftClusterMember>>> statusChangedHandlers;
    private IRaftClusterMember.ReplicationState state;

    private protected RaftClusterMember(ILocalMember localMember, EndPoint endPoint)
    {
        Debug.Assert(localMember is not null);
        Debug.Assert(endPoint is not null);

        this.localMember = localMember;
        EndPoint = endPoint;
        Id = ClusterMemberId.FromEndPoint(endPoint);
        requestTimeout = TimeSpan.FromSeconds(30);
        cachedRemoteAddressAttribute = new(IRaftClusterMember.RemoteAddressMeterAttributeName, endPoint.ToString());
        IsRemote = localMember.Id != Id;
    }

    internal TimeSpan RequestTimeout
    {
        get => requestTimeout;
        init => requestTimeout = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value));
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
    public ClusterMemberStatus Status
    {
        get => IsRemote ? status : ClusterMemberStatus.Available;

#pragma warning disable CS0420
        private protected set => IClusterMember.OnMemberStatusChanged(this, ref status, value, statusChangedHandlers);
#pragma warning restore CS0420
    }

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
    ref IRaftClusterMember.ReplicationState IRaftClusterMember.State => ref state;

    /// <summary>
    /// Cancels pending requests scheduled for this member.
    /// </summary>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    public abstract ValueTask CancelPendingRequestsAsync();

    internal void Touch() => Status = ClusterMemberStatus.Available;

    private protected abstract Task<Result<bool>> VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token);

    /// <inheritdoc/>
    Task<Result<bool>> IRaftClusterMember.VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
        => IsRemote ? VoteAsync(term, lastLogIndex, lastLogTerm, token) : Task.FromResult<Result<bool>>(new() { Term = term, Value = true });

    private protected abstract Task<Result<PreVoteResult>> PreVoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token);

    /// <inheritdoc/>
    Task<Result<PreVoteResult>> IRaftClusterMember.PreVoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
        => IsRemote ? PreVoteAsync(term, lastLogIndex, lastLogTerm, token) : Task.FromResult<Result<PreVoteResult>>(new() { Term = term, Value = PreVoteResult.Accepted });

    private protected abstract Task<Result<HeartbeatResult>> AppendEntriesAsync<TEntry, TList>(long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, IClusterConfiguration config, bool applyConfig, CancellationToken token)
        where TEntry : IRaftLogEntry
        where TList : IReadOnlyList<TEntry>;

    /// <inheritdoc/>
    Task<Result<HeartbeatResult>> IRaftClusterMember.AppendEntriesAsync<TEntry, TList>(long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, IClusterConfiguration config, bool applyConfig, CancellationToken token)
        => IsRemote ? AppendEntriesAsync<TEntry, TList>(term, entries, prevLogIndex, prevLogTerm, commitIndex, config, applyConfig, token) : Task.FromResult<Result<HeartbeatResult>>(new() { Term = term, Value = HeartbeatResult.ReplicatedWithLeaderTerm });

    private protected abstract Task<Result<HeartbeatResult>> InstallSnapshotAsync(long term, IRaftLogEntry snapshot, long snapshotIndex, CancellationToken token);

    /// <inheritdoc/>
    Task<Result<HeartbeatResult>> IRaftClusterMember.InstallSnapshotAsync(long term, IRaftLogEntry snapshot, long snapshotIndex, CancellationToken token)
        => IsRemote ? InstallSnapshotAsync(term, snapshot, snapshotIndex, token) : Task.FromResult<Result<HeartbeatResult>>(new() { Term = term, Value = HeartbeatResult.ReplicatedWithLeaderTerm });

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

    private protected abstract Task<long?> SynchronizeAsync(long commitIndex, CancellationToken token);

    /// <inheritdoc />
    Task<long?> IRaftClusterMember.SynchronizeAsync(long commitIndex, CancellationToken token)
        => IsRemote ? SynchronizeAsync(commitIndex, token) : Task.FromResult<long?>(null);

    /// <inheritdoc />
    public override string ToString() => EndPoint.ToString() ?? Id.ToString();
}
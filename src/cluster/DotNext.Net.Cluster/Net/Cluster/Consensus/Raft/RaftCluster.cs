using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static System.Linq.Enumerable;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Collections.Specialized;
using IO.Log;
using Membership;
using Threading;
using Threading.Tasks;
using IReplicationCluster = Replication.IReplicationCluster;
using Timestamp = Diagnostics.Timestamp;

/// <summary>
/// Represents transport-independent implementation of Raft protocol.
/// </summary>
/// <typeparam name="TMember">The type implementing communication details with remote nodes.</typeparam>
public abstract partial class RaftCluster<TMember> : Disposable, IRaftCluster, IRaftStateMachine, IAsyncDisposable
    where TMember : class, IRaftClusterMember, IDisposable
{
    private readonly bool allowPartitioning, aggressiveStickiness;
    private readonly ElectionTimeout electionTimeoutProvider;
    private readonly CancellationTokenSource transitionCancellation;
    private readonly double heartbeatThreshold, clockDriftBound;
    private readonly Random random;
    private readonly TaskCompletionSource readinessProbe;

    private ClusterMemberId localMemberId;
    private bool standbyNode;
    private AsyncLock transitionSync;  // used to synchronize state transitions

    [SuppressMessage("Usage", "CA2213", Justification = "Disposed correctly but cannot be recognized by .NET Analyzer")]
    private volatile RaftState? state;
    private volatile TMember? leader;
    private volatile TaskCompletionSource<TMember?> electionEvent;
    private InvocationList<Action<RaftCluster<TMember>, TMember?>> leaderChangedHandlers;
    private InvocationList<Action<RaftCluster<TMember>, TMember>> replicationHandlers;
    private volatile int electionTimeout;
    private IPersistentState auditTrail;
    private Timestamp lastUpdated; // volatile

    /// <summary>
    /// Initializes a new cluster manager for the local node.
    /// </summary>
    /// <param name="config">The configuration of the local node.</param>
    protected RaftCluster(IClusterMemberConfiguration config)
    {
        electionTimeoutProvider = config.ElectionTimeout;
        random = new();
        electionTimeout = electionTimeoutProvider.RandomTimeout(random);
        allowPartitioning = config.Partitioning;
        members = MemberList.Empty;
        transitionSync = AsyncLock.Exclusive();
        transitionCancellation = new CancellationTokenSource();
        LifecycleToken = transitionCancellation.Token;
        auditTrail = new ConsensusOnlyState();
        heartbeatThreshold = config.HeartbeatThreshold;
        standbyNode = config.Standby;
        clockDriftBound = config.ClockDriftBound;
        readinessProbe = new(TaskCreationOptions.RunContinuationsAsynchronously);
        localMemberId = new(random);
        aggressiveStickiness = config.AggressiveLeaderStickiness;
        electionEvent = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Gets logger used by this object.
    /// </summary>
    [CLSCompliant(false)]
    protected virtual ILogger Logger => NullLogger.Instance;

    /// <summary>
    /// Gets information the current member.
    /// </summary>
    public ref readonly ClusterMemberId LocalMemberId => ref localMemberId;

    /// <inheritdoc />
    ILogger IRaftStateMachine.Logger => Logger;

    /// <inheritdoc />
    void IRaftStateMachine.UpdateLeaderStickiness() => Timestamp.Refresh(ref lastUpdated);

    /// <summary>
    /// Gets election timeout used by the local member.
    /// </summary>
    public TimeSpan ElectionTimeout => TimeSpan.FromMilliseconds(electionTimeout);

    /// <summary>
    /// Represents a task indicating that the current node is ready to serve requests.
    /// </summary>
    public Task Readiness => readinessProbe.Task;

    private TimeSpan HeartbeatTimeout => TimeSpan.FromMilliseconds(electionTimeout * heartbeatThreshold);

    private TimeSpan LeaderLeaseDuration => TimeSpan.FromMilliseconds(electionTimeout / clockDriftBound);

    /// <summary>
    /// Indicates that local member is a leader.
    /// </summary>
    protected bool IsLeaderLocal => state is LeaderState;

    /// <summary>
    /// Gets the lease that can be used for linearizable read.
    /// </summary>
    public ILeaderLease? Lease => state as LeaderState;

    /// <summary>
    /// Gets the cancellation token that tracks the leader state of the current node.
    /// </summary>
    public CancellationToken LeadershipToken => (state as LeaderState)?.LeadershipToken ?? new(true);

    /// <summary>
    /// Associates audit trail with the current instance.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public IPersistentState AuditTrail
    {
        get => auditTrail;
        set => auditTrail = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets configuration storage.
    /// </summary>
    protected abstract IClusterConfigurationStorage ConfigurationStorage { get; }

    /// <summary>
    /// Gets token that can be used for all internal asynchronous operations.
    /// </summary>
    protected CancellationToken LifecycleToken { get; } // cached to avoid ObjectDisposedException that may be caused by CTS.Token

    /// <summary>
    /// Gets members of Raft-based cluster.
    /// </summary>
    /// <returns>A collection of cluster member.</returns>
    public IReadOnlyCollection<TMember> Members => state is null ? Array.Empty<TMember>() : members;

    /// <inheritdoc />
    IReadOnlyCollection<IRaftClusterMember> IRaftCluster.Members => Members;

    /// <inheritdoc />
    IReadOnlyCollection<IRaftClusterMember> IRaftStateMachine.Members => members;

    /// <summary>
    /// Establishes metrics collector.
    /// </summary>
    public MetricsCollector? Metrics
    {
        protected get;
        set;
    }

    /// <summary>
    /// Gets Term value maintained by local member.
    /// </summary>
    public long Term => auditTrail.Term;

    /// <summary>
    /// An event raised when leader has been changed.
    /// </summary>
    public event Action<RaftCluster<TMember>, TMember?> LeaderChanged
    {
        add => leaderChangedHandlers += value;
        remove => leaderChangedHandlers -= value;
    }

    /// <inheritdoc />
    event Action<ICluster, IClusterMember?> ICluster.LeaderChanged
    {
        add => leaderChangedHandlers += value;
        remove => leaderChangedHandlers -= value;
    }

    /// <summary>
    /// Represents an event raised when the local node completes its replication with another
    /// node.
    /// </summary>
    public event Action<RaftCluster<TMember>, TMember> ReplicationCompleted
    {
        add => replicationHandlers += value;
        remove => replicationHandlers -= value;
    }

    /// <inheritdoc />
    event Action<IReplicationCluster, IClusterMember> IReplicationCluster.ReplicationCompleted
    {
        add => replicationHandlers += value;
        remove => replicationHandlers -= value;
    }

    /// <inheritdoc/>
    IClusterMember? ICluster.Leader => Leader;

    /// <summary>
    /// Gets leader of the cluster.
    /// </summary>
    public TMember? Leader
    {
        get => leader;
        private set
        {
            var oldLeader = Interlocked.Exchange(ref leader, value);
            if (!ReferenceEquals(oldLeader, value) && !leaderChangedHandlers.IsEmpty)
            {
                Interlocked.Exchange(ref electionEvent, new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult(value);
                leaderChangedHandlers.Invoke(this, value);
            }
        }
    }

    /// <summary>
    /// Waits for the leader election asynchronously.
    /// </summary>
    /// <param name="timeout">The time to wait; or <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The elected leader or <see langword="null"/> if the cluster losts the leader.</returns>
    /// <exception cref="TimeoutException">The operation is timed out.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The local node is disposed.</exception>
    public Task<TMember?> WaitForLeaderAsync(TimeSpan timeout, CancellationToken token = default)
    {
        var leader = this.leader;
        return leader is null ? electionEvent.Task.WaitAsync(timeout, token) : Task.FromResult<TMember?>(leader);
    }

    /// <inheritdoc />
    Task<IClusterMember?> ICluster.WaitForLeaderAsync(TimeSpan timeout, CancellationToken token)
        => Unsafe.As<Task<IClusterMember?>>(WaitForLeaderAsync(timeout, token)); // TODO: Dirty hack but acceptable because there is no covariance with tasks

    private FollowerState CreateInitialState()
        => new FollowerState(this) { Metrics = Metrics }.StartServing(ElectionTimeout, LifecycleToken);

    private ValueTask UnfreezeAsync()
    {
        return readinessProbe.Task.IsCompleted ? ValueTask.CompletedTask : UnfreezeCoreAsync();

        async ValueTask UnfreezeCoreAsync()
        {
            // ensure that local member has been received
            foreach (var member in members.Values)
            {
                if (member.Id == localMemberId)
                {
                    if (!standbyNode)
                    {
                        var newState = new FollowerState(this);
                        using var currentState = state;
                        await (currentState?.StopAsync() ?? Task.CompletedTask).ConfigureAwait(false);
                        state = newState.StartServing(ElectionTimeout, LifecycleToken);
                    }

                    readinessProbe.TrySetResult();
                }
            }
        }
    }

    /// <summary>
    /// Starts serving local member.
    /// </summary>
    /// <param name="token">The token that can be used to cancel initialization process.</param>
    /// <returns>The task representing asynchronous execution of the method.</returns>
    /// <seealso cref="StartFollowing"/>
    public virtual async Task StartAsync(CancellationToken token)
    {
        await auditTrail.InitializeAsync(token).ConfigureAwait(false);

        var localMember = GetLocalMember();

        // local member is known then turn readiness probe into signalled state and start serving the messages from the cluster
        if (localMember is not null)
        {
            localMemberId = localMember.Id;
            state = standbyNode ? new StandbyState(this) : new FollowerState(this);
            readinessProbe.TrySetResult();
        }
        else
        {
            // local member is not known. Start in frozen state and wait when the current node will be added to the cluster
            state = new StandbyState(this);
        }

        TMember? GetLocalMember()
        {
            foreach (var member in members.Values)
            {
                if (!member.IsRemote)
                    return member;
            }

            return null;
        }
    }

    /// <summary>
    /// Starts Follower timer.
    /// </summary>
    protected void StartFollowing() => (state as FollowerState)?.StartServing(ElectionTimeout, LifecycleToken);

    /// <summary>
    /// Turns this node into regular state when the node can be elected as leader.
    /// </summary>
    /// <remarks>
    /// Initially, the node can be started in standby mode when it cannot be elected as a leader.
    /// This can be helpful if you need to wait for full replication with existing leader node.
    /// When replication finished, you can turn this node into regular state.
    /// </remarks>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of this operation.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    public async ValueTask TurnIntoRegularNodeAsync(CancellationToken token)
    {
        ThrowIfDisposed();
        if (standbyNode && state is StandbyState)
        {
            using var tokenSource = token.LinkTo(LifecycleToken);
            using var transitionLock = await transitionSync.AcquireAsync(token).ConfigureAwait(false);
            standbyNode = false;
            state = CreateInitialState();
        }
    }

    private async Task CancelPendingRequestsAsync()
    {
        ICollection<Task> tasks = new LinkedList<Task>();
        foreach (var member in members.Values)
            tasks.Add(member.CancelPendingRequestsAsync().AsTask());

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Logger.FailedToCancelPendingRequests(e);
        }
    }

    /// <summary>
    /// Stops serving local member.
    /// </summary>
    /// <param name="token">The token that can be used to cancel shutdown process.</param>
    /// <returns>The task representing asynchronous execution of the method.</returns>
    public virtual async Task StopAsync(CancellationToken token)
    {
        if (transitionCancellation.IsCancellationRequested)
            return;
        transitionCancellation.Cancel(false);
        await CancelPendingRequestsAsync().ConfigureAwait(false);
        leader = null;
        using (await transitionSync.AcquireAsync(token).ConfigureAwait(false))
        {
            var currentState = Interlocked.Exchange(ref state, null);
            if (currentState is not null)
            {
                await currentState.StopAsync().ConfigureAwait(false);
                currentState.Dispose();
            }
        }
    }

    private ValueTask StepDown(long newTerm)
    {
        return newTerm > auditTrail.Term ? UpdateTermAndStepDownAsync(newTerm) : StepDown();

        async ValueTask UpdateTermAndStepDownAsync(long newTerm)
        {
            await auditTrail.UpdateTermAsync(newTerm, true).ConfigureAwait(false);
            await StepDown().ConfigureAwait(false);
        }
    }

    [AsyncStateMachine(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask StepDown()
    {
        Logger.DowngradingToFollowerState();
        switch (state)
        {
            case FollowerState followerState:
                followerState.Refresh();
                break;
            case LeaderState leaderState:
                var newState = new FollowerState(this) { Metrics = Metrics };
                await leaderState.StopAsync().ConfigureAwait(false);
                state = newState.StartServing(ElectionTimeout, LifecycleToken);
                leaderState.Dispose();
                Metrics?.MovedToFollowerState();
                break;
            case CandidateState candidateState:
                newState = new FollowerState(this) { Metrics = Metrics };
                await candidateState.StopAsync().ConfigureAwait(false);
                state = newState.StartServing(ElectionTimeout, LifecycleToken);
                candidateState.Dispose();
                Metrics?.MovedToFollowerState();
                break;
        }

        Logger.DowngradedToFollowerState();
    }

    /// <summary>
    /// Handles InstallSnapshot message received from remote cluster member.
    /// </summary>
    /// <typeparam name="TSnapshot">The type of snapshot record.</typeparam>
    /// <param name="sender">The sender of the snapshot message.</param>
    /// <param name="senderTerm">Term value provided by InstallSnapshot message sender.</param>
    /// <param name="snapshot">The snapshot to be installed into local audit trail.</param>
    /// <param name="snapshotIndex">The index of the last log entry included in the snapshot.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if snapshot is installed successfully; <see langword="false"/> if snapshot is outdated.</returns>
    protected async Task<Result<bool>> InstallSnapshotAsync<TSnapshot>(ClusterMemberId sender, long senderTerm, TSnapshot snapshot, long snapshotIndex, CancellationToken token)
        where TSnapshot : notnull, IRaftLogEntry
    {
        using var tokenSource = token.LinkTo(LifecycleToken);
        using var transitionLock = await transitionSync.AcquireAsync(token).ConfigureAwait(false);
        var currentTerm = auditTrail.Term;
        if (snapshot.IsSnapshot && senderTerm >= currentTerm && snapshotIndex > auditTrail.LastCommittedEntryIndex)
        {
            Timestamp.Refresh(ref lastUpdated);
            await StepDown(senderTerm).ConfigureAwait(false);
            Leader = TryGetMember(sender);
            await auditTrail.AppendAsync(snapshot, snapshotIndex, token).ConfigureAwait(false);
            return new(currentTerm, true);
        }

        return new(currentTerm, false);
    }

    /// <summary>
    /// Handles AppendEntries message received from remote cluster member.
    /// </summary>
    /// <typeparam name="TEntry">The actual type of the log entry returned by the supplier.</typeparam>
    /// <param name="sender">The sender of the replica message.</param>
    /// <param name="senderTerm">Term value provided by Heartbeat message sender.</param>
    /// <param name="entries">The stateful function that provides entries to be committed locally.</param>
    /// <param name="prevLogIndex">Index of log entry immediately preceding new ones.</param>
    /// <param name="prevLogTerm">Term of <paramref name="prevLogIndex"/> entry.</param>
    /// <param name="commitIndex">The last entry known to be committed on the sender side.</param>
    /// <param name="config">The list of cluster members.</param>
    /// <param name="applyConfig">
    /// <see langword="true"/> to inform that the receiver must apply previously proposed configuration;
    /// <see langword="false"/> to propose a new configuration.
    /// </param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if log entry is committed successfully; <see langword="false"/> if preceding is not present in local audit trail.</returns>
    protected async Task<Result<bool>> AppendEntriesAsync<TEntry>(ClusterMemberId sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, IClusterConfiguration config, bool applyConfig, CancellationToken token)
        where TEntry : notnull, IRaftLogEntry
    {
        using var tokenSource = token.LinkTo(LifecycleToken);
        using var transitionLock = await transitionSync.AcquireAsync(token).ConfigureAwait(false);
        var result = false;
        var currentTerm = auditTrail.Term;
        if (currentTerm <= senderTerm)
        {
            Timestamp.Refresh(ref lastUpdated);
            await StepDown(senderTerm).ConfigureAwait(false);
            var senderMember = TryGetMember(sender);
            Leader = senderMember;
            if (await auditTrail.ContainsAsync(prevLogIndex, prevLogTerm, token).ConfigureAwait(false))
            {
                var emptySet = entries.RemainingCount is 0L;

                // prevent Follower state transition during processing of received log entries
                using (new FollowerState.TransitionSuppressionScope(state as FollowerState))
                {
                    /*
                    * AppendAsync is called with skipCommitted=true because HTTP response from the previous
                    * replication might fail but the log entry was committed by the local node.
                    * In this case the leader repeat its replication from the same prevLogIndex which is already committed locally.
                    * skipCommitted=true allows to skip the passed committed entry and append uncommitted entries.
                    * If it is 'false' then the method will throw the exception and the node becomes unavailable in each replication cycle.
                    */
                    await auditTrail.AppendAndCommitAsync(entries, prevLogIndex + 1L, true, commitIndex, token).ConfigureAwait(false);
                    result = true;

                    // This node is in sync with the leader and no entries arrived
                    if (emptySet)
                    {
                        if (senderMember is not null && !replicationHandlers.IsEmpty)
                            replicationHandlers.Invoke(this, senderMember);

                        await UnfreezeAsync().ConfigureAwait(false);
                    }

                    // process configuration
                    var fingerprint = (ConfigurationStorage.ProposedConfiguration ?? ConfigurationStorage.ActiveConfiguration).Fingerprint;
                    switch ((config.Fingerprint == fingerprint, applyConfig))
                    {
                        case (true, true):
                            await ConfigurationStorage.ApplyAsync(token).ConfigureAwait(false);
                            break;
                        case (true, false):
                            break;
                        case (false, false):
                            await ConfigurationStorage.ProposeAsync(config).ConfigureAwait(false);
                            break;
                        case (false, true):
                            result = false;
                            break;
                    }
                }
            }
        }

        return new(currentTerm, result);
    }

    /// <summary>
    /// Receives preliminary vote from the potential Candidate in the cluster.
    /// </summary>
    /// <param name="nextTerm">Caller's current term + 1.</param>
    /// <param name="lastLogIndex">Index of candidate's last log entry.</param>
    /// <param name="lastLogTerm">Term of candidate's last log entry.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>Pre-vote result received from the member.</returns>
    protected async Task<Result<PreVoteResult>> PreVoteAsync(long nextTerm, long lastLogIndex, long lastLogTerm, CancellationToken token)
    {
        PreVoteResult result;
        long currentTerm;

        // PreVote doesn't cause transition to another Raft state so locking not needed
        var tokenSource = token.LinkTo(LifecycleToken);
        try
        {
            currentTerm = auditTrail.Term;

            // provide leader stickiness
            if (aggressiveStickiness && state is LeaderState)
            {
                result = PreVoteResult.RejectedByLeader;
            }
            else if (Timestamp.VolatileRead(ref lastUpdated).Elapsed >= ElectionTimeout && currentTerm <= nextTerm && await auditTrail.IsUpToDateAsync(lastLogIndex, lastLogTerm, token).ConfigureAwait(false))
            {
                result = PreVoteResult.Accepted;
            }
            else
            {
                result = PreVoteResult.RejectedByFollower;
            }
        }
        finally
        {
            tokenSource?.Dispose();
        }

        return new(currentTerm, result);
    }

    // pre-vote logic that allow to decide about transition to candidate state
    private async Task<bool> PreVoteAsync(long currentTerm)
    {
        var lastIndex = auditTrail.LastUncommittedEntryIndex;
        var lastTerm = await auditTrail.GetTermAsync(lastIndex, LifecycleToken).ConfigureAwait(false);
        var votes = 0;

        // analyze responses
        await foreach (var response in SendRequestsAsync(currentTerm, lastIndex, lastTerm).ConfigureAwait(false))
        {
            Debug.Assert(response.IsCompleted);

            try
            {
                switch (response.GetAwaiter().GetResult().Value)
                {
                    case PreVoteResult.Accepted:
                        votes++;
                        break;
                    case PreVoteResult.RejectedByFollower:
                        votes--;
                        break;
                    case PreVoteResult.RejectedByLeader:
                        votes = short.MinValue;
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (MemberUnavailableException)
            {
                votes -= 1;
            }
            finally
            {
                response.Dispose();
            }
        }

        return votes > 0;

        IAsyncEnumerable<Task<Result<PreVoteResult>>> SendRequestsAsync(long currentTerm, long lastIndex, long lastTerm)
        {
            var members = this.members;
            var responses = new TaskCompletionPipe<Task<Result<PreVoteResult>>>(members.Count);
            foreach (var member in members.Values)
                responses.Add(member.PreVoteAsync(currentTerm, lastIndex, lastTerm, LifecycleToken));

            responses.Complete();
            return responses;
        }
    }

    /// <summary>
    /// Votes for the new candidate.
    /// </summary>
    /// <param name="sender">The vote sender.</param>
    /// <param name="senderTerm">Term value provided by sender of the request.</param>
    /// <param name="lastLogIndex">Index of candidate's last log entry.</param>
    /// <param name="lastLogTerm">Term of candidate's last log entry.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if local node accepts new leader in the cluster; otherwise, <see langword="false"/>.</returns>
    protected async Task<Result<bool>> VoteAsync(ClusterMemberId sender, long senderTerm, long lastLogIndex, long lastLogTerm, CancellationToken token)
    {
        var result = false;
        var currentTerm = auditTrail.Term;

        // provide leader stickiness
        if (currentTerm > senderTerm || Timestamp.VolatileRead(ref lastUpdated).Elapsed < ElectionTimeout)
            goto exit;

        using (var tokenSource = token.LinkTo(LifecycleToken))
        using (var transitionLock = await transitionSync.AcquireAsync(token).ConfigureAwait(false))
        {
            currentTerm = auditTrail.Term;

            if (currentTerm > senderTerm)
            {
                goto exit;
            }
            else if (currentTerm != senderTerm)
            {
                Leader = null;
                await StepDown(senderTerm).ConfigureAwait(false);
            }
            else if (state is FollowerState follower)
            {
                follower.Refresh();
            }
            else if (state is StandbyState)
            {
                Metrics?.ReportHeartbeat();
            }
            else
            {
                goto exit;
            }

            if (auditTrail.IsVotedFor(sender) && await auditTrail.IsUpToDateAsync(lastLogIndex, lastLogTerm, token).ConfigureAwait(false))
            {
                await auditTrail.UpdateVotedForAsync(sender).ConfigureAwait(false);
                result = true;
            }
        }

    exit:
        return new(currentTerm, result);
    }

    /// <summary>
    /// Revokes leadership of the local node.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/>, if leadership is revoked successfully; otherwise, <see langword="false"/>.</returns>
    protected async Task<bool> ResignAsync(CancellationToken token)
    {
        if (state is StandbyState)
            return false;

        using var tokenSource = token.LinkTo(LifecycleToken);
        using var lockHolder = await transitionSync.AcquireAsync(token).ConfigureAwait(false);
        bool result;
        if (state is LeaderState leaderState)
        {
            await leaderState.StopAsync().ConfigureAwait(false);
            state = new FollowerState(this) { Metrics = Metrics }.StartServing(ElectionTimeout, LifecycleToken);
            leaderState.Dispose();
            Leader = null;
            result = true;
        }
        else
        {
            result = false;
        }

        return result;
    }

    /// <summary>
    /// Processes <see cref="IRaftClusterMember.SynchronizeAsync(long, CancellationToken)"/>
    /// request.
    /// </summary>
    /// <param name="commitIndex">The index of the last committed log entry on the sender side.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The index of the last committed log entry known by the leader.</returns>
    protected async Task<long?> SynchronizeAsync(long commitIndex, CancellationToken token)
    {
        using var tokenSource = token.LinkTo(LifecycleToken);

        // do not execute the next round of heartbeats if the sender is already in sync with the leader
        if (state is LeaderState leaderState)
        {
            if (commitIndex != auditTrail.LastCommittedEntryIndex)
                await leaderState.ForceReplicationAsync(token).ConfigureAwait(false);

            return ReferenceEquals(state, leaderState) ? auditTrail.LastCommittedEntryIndex : null;
        }

        return null;
    }

    /// <summary>
    /// Ensures linearizable read from underlying state machine.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public async ValueTask ApplyReadBarrierAsync(CancellationToken token = default)
    {
        for (; ; )
        {
            if (state is LeaderState leaderState)
            {
                await leaderState.ForceReplicationAsync(token).ConfigureAwait(false);
            }
            else if (this.leader is TMember leader)
            {
                var commitIndex = await leader.SynchronizeAsync(auditTrail.LastCommittedEntryIndex, token).ConfigureAwait(false);
                if (commitIndex is null)
                    continue;

                await auditTrail.WaitForCommitAsync(commitIndex.GetValueOrDefault(), token).ConfigureAwait(false);
            }
            else
            {
                throw new InvalidOperationException(ExceptionMessages.LeaderIsUnavailable);
            }

            break;
        }
    }

    /// <inheritdoc/>
    async Task<bool> ICluster.ResignAsync(CancellationToken token)
    {
        if (await ResignAsync(token).ConfigureAwait(false))
        {
            return true;
        }
        else
        {
            var leader = Leader;
            return leader is not null && await leader.ResignAsync(token).ConfigureAwait(false);
        }
    }

    private async ValueTask MoveToStandbyState()
    {
        Leader = null;
        if (Interlocked.Exchange(ref state, new StandbyState(this)) is RaftState currentState)
        {
            await currentState.StopAsync().ConfigureAwait(false);
            currentState.Dispose();
        }
    }

    /// <inheritdoc />
    async void IRaftStateMachine.MoveToFollowerState(bool randomizeTimeout, long? newTerm)
    {
        Debug.Assert(state is not StandbyState);

        var lockHolder = default(AsyncLock.Holder);
        try
        {
            lockHolder = await transitionSync.TryAcquireAsync(LifecycleToken).SuppressDisposedStateOrCancellation().ConfigureAwait(false);
            if (lockHolder)
            {
                if (randomizeTimeout)
                    electionTimeout = electionTimeoutProvider.RandomTimeout(random);

                await (newTerm.HasValue ? StepDown(newTerm.GetValueOrDefault()) : StepDown()).ConfigureAwait(false);
            }
        }
        catch (Exception e)
        {
            Logger.TransitionToFollowerStateFailed(e);
            await MoveToStandbyState().ConfigureAwait(false);
        }
        finally
        {
            lockHolder.Dispose();
        }
    }

    /// <inheritdoc />
    async void IRaftStateMachine.MoveToCandidateState()
    {
        Debug.Assert(state is not StandbyState);

        var lockHolder = default(AsyncLock.Holder);
        try
        {
            var currentTerm = auditTrail.Term;
            var readyForTransition = await PreVoteAsync(currentTerm).ConfigureAwait(false);
            lockHolder = await transitionSync.TryAcquireAsync(LifecycleToken).SuppressDisposedStateOrCancellation().ConfigureAwait(false);
            if (lockHolder && state is FollowerState { IsExpired: true } followerState)
            {
                Logger.TransitionToCandidateStateStarted();

                // if term changed after lock then assumes that leader will be updated soon
                if (currentTerm == auditTrail.Term)
                    Leader = null;
                else
                    readyForTransition = false;

                if (readyForTransition)
                {
                    followerState.Dispose();

                    // vote for self
                    state = new CandidateState(this, await auditTrail.IncrementTermAsync(localMemberId).ConfigureAwait(false)).StartVoting(electionTimeout, auditTrail);
                    Metrics?.MovedToCandidateState();
                    Logger.TransitionToCandidateStateCompleted();
                }
                else
                {
                    // resume follower state
                    followerState.StartServing(ElectionTimeout, LifecycleToken);
                    Logger.DowngradedToFollowerState();
                }
            }
        }
        catch (Exception e)
        {
            Logger.TransitionToCandidateStateFailed(e);
            await MoveToStandbyState().ConfigureAwait(false);
        }
        finally
        {
            lockHolder.Dispose();
        }
    }

    /// <inheritdoc />
    async void IRaftStateMachine.MoveToLeaderState(IRaftClusterMember newLeader)
    {
        Debug.Assert(state is not StandbyState);
        var lockHolder = default(AsyncLock.Holder);

        try
        {
            Logger.TransitionToLeaderStateStarted();
            lockHolder = await transitionSync.TryAcquireAsync(LifecycleToken).SuppressDisposedStateOrCancellation().ConfigureAwait(false);
            long currentTerm;
            if (lockHolder && state is CandidateState candidateState && candidateState.Term == (currentTerm = auditTrail.Term))
            {
                candidateState.Dispose();
                state = new LeaderState(this, allowPartitioning, currentTerm, LeaderLeaseDuration) { Metrics = Metrics }
                    .StartLeading(HeartbeatTimeout, auditTrail, ConfigurationStorage, LifecycleToken);
                await auditTrail.AppendNoOpEntry(LifecycleToken).ConfigureAwait(false);
                Leader = newLeader as TMember;
                Metrics?.MovedToLeaderState();
                Logger.TransitionToLeaderStateCompleted();
            }
        }
        catch (Exception e)
        {
            Logger.TransitionToLeaderStateFailed(e);
            await MoveToStandbyState().ConfigureAwait(false);
        }
        finally
        {
            lockHolder.Dispose();
        }
    }

    /// <summary>
    /// Forces replication.
    /// </summary>
    /// <param name="token">The token that can be used to cancel waiting.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="InvalidOperationException">The local cluster member is not a leader.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public Task ForceReplicationAsync(CancellationToken token = default)
        => state is LeaderState leaderState
            ? leaderState.ForceReplicationAsync(token)
            : Task.FromException(new InvalidOperationException(ExceptionMessages.LocalNodeNotLeader));

    /// <summary>
    /// Appends a new log entry and ensures that it is replicated and committed.
    /// </summary>
    /// <typeparam name="TEntry">The type of the log entry.</typeparam>
    /// <param name="entry">The log entry to be added.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if the appended log entry has been committed by the majority of nodes; <see langword="false"/> if retry is required.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The current node is not a leader.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public async Task<bool> ReplicateAsync<TEntry>(TEntry entry, CancellationToken token)
        where TEntry : notnull, IRaftLogEntry
    {
        ThrowIfDisposed();

        using var tokenSource = token.LinkTo(LifecycleToken);

        // 1 - append entry to the log
        var index = await auditTrail.AppendAsync(entry, token).ConfigureAwait(false);

        // 2 - force replication
        await ForceReplicationAsync(token).ConfigureAwait(false);

        // 3 - wait for commit
        await auditTrail.WaitForCommitAsync(index, token).ConfigureAwait(false);

        return auditTrail.Term == entry.Term;
    }

    private TMember? TryGetPeer(EndPoint peer)
    {
        foreach (var member in members.Values)
        {
            if (Equals(member.EndPoint, peer))
                return member;
        }

        return null;
    }

    /// <inheritdoc />
    IRaftClusterMember? IPeerMesh<IRaftClusterMember>.TryGetPeer(EndPoint peer) => TryGetPeer(peer);

    /// <inheritdoc />
    IClusterMember? IPeerMesh<IClusterMember>.TryGetPeer(EndPoint peer) => TryGetPeer(peer);

    /// <inheritdoc />
    IReadOnlySet<EndPoint> IPeerMesh.Peers => ImmutableHashSet.CreateRange(members.Values.Select(static m => m.EndPoint));

    private void Cleanup()
    {
        Dispose(Interlocked.Exchange(ref members, MemberList.Empty));
        transitionCancellation.Dispose();
        transitionSync.Dispose();
        leader = null;
        Interlocked.Exchange(ref state, null)?.Dispose();
        TrySetDisposedException(readinessProbe);
        ConfigurationStorage.Dispose();

        memberAddedHandlers = memberRemovedHandlers = default;
        leaderChangedHandlers = default;
        TrySetDisposedException(electionEvent);
    }

    /// <summary>
    /// Releases managed and unmanaged resources associated with this object.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> if called from <see cref="Disposable.Dispose()"/>; <see langword="false"/> if called from finalizer <see cref="Disposable.Finalize()"/>.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (!transitionCancellation.IsCancellationRequested)
                Logger.StopAsyncWasNotCalled();
            Cleanup();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        Cleanup();
    }

    /// <inheritdoc />
    public new ValueTask DisposeAsync() => base.DisposeAsync();
}
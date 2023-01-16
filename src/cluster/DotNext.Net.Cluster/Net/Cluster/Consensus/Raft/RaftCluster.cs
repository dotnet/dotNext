using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static System.Linq.Enumerable;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Collections.Specialized;
using Diagnostics;
using Extensions;
using IO.Log;
using Membership;
using Threading;
using Threading.Tasks;
using IReplicationCluster = Replication.IReplicationCluster;

/// <summary>
/// Represents transport-independent implementation of Raft protocol.
/// </summary>
/// <typeparam name="TMember">The type implementing communication details with remote nodes.</typeparam>
public abstract partial class RaftCluster<TMember> : Disposable, IUnresponsiveClusterMemberRemovalSupport, IStandbyModeSupport, IRaftStateMachine<TMember>, IAsyncDisposable
    where TMember : class, IRaftClusterMember, IDisposable
{
    private readonly bool allowPartitioning, aggressiveStickiness;
    private readonly ElectionTimeout electionTimeoutProvider;
    private readonly CancellationTokenSource transitionCancellation;
    private readonly double heartbeatThreshold, clockDriftBound;
    private readonly Random random;
    private readonly TaskCompletionSource readinessProbe;
    private readonly bool standbyNode;
    private ClusterMemberId localMemberId;

    private AsyncLock transitionSync;  // used to synchronize state transitions
    private volatile RaftState<TMember> state;
    private volatile TaskCompletionSource<TMember> electionEvent;
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
        transitionCancellation = new();
        LifecycleToken = transitionCancellation.Token;
        auditTrail = new ConsensusOnlyState();
        heartbeatThreshold = config.HeartbeatThreshold;
        standbyNode = config.Standby;
        clockDriftBound = config.ClockDriftBound;
        readinessProbe = new(TaskCreationOptions.RunContinuationsAsynchronously);
        localMemberId = config.Id ?? new(random);
        aggressiveStickiness = config.AggressiveLeaderStickiness;
        electionEvent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        state = new StandbyState<TMember>(this);
        EndPointComparer = config.EndPointComparer;
    }

    /// <summary>
    /// Gets or sets failure detector to be used by the leader node to detect and remove unresponsive followers.
    /// </summary>
    public Func<TMember, IFailureDetector>? FailureDetectorFactory
    {
        get;
        init;
    }

    /// <inheritdoc/>
    Func<IClusterMember, IFailureDetector>? IUnresponsiveClusterMemberRemovalSupport.FailureDetectorFactory
    {
        init => FailureDetectorFactory = value;
    }

    /// <summary>
    /// Gets the comparer for <see cref="EndPoint"/> type.
    /// </summary>
    protected IEqualityComparer<EndPoint> EndPointComparer { get; }

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
    /// Indicates that the local member is a leader.
    /// </summary>
    [Obsolete("Use LeadershipToken property instead.")]
    protected bool IsLeaderLocal => state is LeaderState<TMember>;

    /// <summary>
    /// Gets the lease that can be used for linearizable read.
    /// </summary>
    public ILeaderLease? Lease => (state as LeaderState<TMember>)?.Lease;

    /// <summary>
    /// Gets the cancellation token that tracks the leader state of the current node.
    /// </summary>
    public CancellationToken LeadershipToken => (state as LeaderState<TMember>)?.LeadershipToken ?? new(true);

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
    public IReadOnlyCollection<TMember> Members => members;

    /// <inheritdoc />
    IReadOnlyCollection<IRaftClusterMember> IRaftCluster.Members => Members;

    /// <summary>
    /// Establishes metrics collector.
    /// </summary>
    public MetricsCollector? Metrics
    {
        protected get;
        init;
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
        get => electionEvent.Task is { IsCompletedSuccessfully: true } task ? task.Result : null;
        private set
        {
            var electionEventCopy = electionEvent;
            bool raiseEventHandlers;

            switch ((electionEventCopy.Task.IsCompleted, value is null))
            {
                case (true, true):
                    TaskCompletionSource<TMember> newEvent = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    raiseEventHandlers = ReferenceEquals(Interlocked.CompareExchange(ref electionEvent, newEvent, electionEventCopy), electionEventCopy);
                    break;
                case (false, false):
                    Debug.Assert(value is not null);
                    raiseEventHandlers = electionEventCopy.TrySetResult(value);
                    break;
                case (true, false) when !ReferenceEquals(electionEventCopy.Task.Result, value):
                    Debug.Assert(value is not null);
                    newEvent = new();
                    newEvent.SetResult(value);
                    raiseEventHandlers = ReferenceEquals(Interlocked.CompareExchange(ref electionEvent, newEvent, electionEventCopy), electionEventCopy);
                    break;
                default:
                    raiseEventHandlers = false;
                    break;
            }

            if (raiseEventHandlers)
                leaderChangedHandlers.Invoke(this, value);
        }
    }

    /// <summary>
    /// Waits for the leader election asynchronously.
    /// </summary>
    /// <param name="timeout">The time to wait; or <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The elected leader.</returns>
    /// <exception cref="TimeoutException">The operation is timed out.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The local node is disposed.</exception>
    public Task<TMember> WaitForLeaderAsync(TimeSpan timeout, CancellationToken token = default)
        => electionEvent.Task.WaitAsync(timeout, token);

    /// <inheritdoc />
    Task<IClusterMember> ICluster.WaitForLeaderAsync(TimeSpan timeout, CancellationToken token)
        => Unsafe.As<Task<IClusterMember>>(WaitForLeaderAsync(timeout, token)); // TODO: Dirty hack but acceptable because there is no covariance with tasks

    private ValueTask UnfreezeAsync()
    {
        ValueTask result;

        // ensure that local member has been received
        TMember? localMember;
        if (readinessProbe.Task.IsCompleted || (localMember = TryGetLocalMember()) is null)
        {
            result = ValueTask.CompletedTask;
        }
        else if (standbyNode)
        {
            readinessProbe.TrySetResult();
            result = new(readinessProbe.Task);
        }
        else
        {
            result = UnfreezeCoreAsync();
        }

        return result;

        async ValueTask UnfreezeCoreAsync()
        {
            var newState = new FollowerState<TMember>(this) { Metrics = Metrics };
            await UpdateStateAsync(newState).ConfigureAwait(false);
            newState.StartServing(ElectionTimeout, LifecycleToken);
            readinessProbe.TrySetResult();
        }
    }

    private TMember? TryGetLocalMember() => members.Values.FirstOrDefault(static m => m.IsRemote is false);

    /// <summary>
    /// Starts serving local member.
    /// </summary>
    /// <param name="localMemberDetector">A predicate that allows to resolve local cluster member.</param>
    /// <param name="token">The token that can be used to cancel initialization process.</param>
    /// <returns>The task representing asynchronous execution of the method.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="localMemberDetector"/> is <see langword="null"/>.</exception>
    /// <seealso cref="StartFollowing"/>
    protected async Task StartAsync(Func<TMember, CancellationToken, ValueTask<bool>> localMemberDetector, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(localMemberDetector);

        await auditTrail.InitializeAsync(token).ConfigureAwait(false);

        // local member is known then turn readiness probe into signalled state and start serving the messages from the cluster
        foreach (var member in members.Values)
        {
            if (await localMemberDetector(member, token).ConfigureAwait(false))
            {
                localMemberId = member.Id;
                state = standbyNode ? new StandbyState<TMember>(this) : new FollowerState<TMember>(this);
                readinessProbe.TrySetResult();
                return;
            }
        }

        // local member is not known. Start in frozen state and wait when the current node will be added to the cluster
        state = new StandbyState<TMember>(this);
    }

    /// <summary>
    /// Starts Follower timer.
    /// </summary>
    protected void StartFollowing() => (state as FollowerState<TMember>)?.StartServing(ElectionTimeout, LifecycleToken);

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
    [Obsolete("Use RevertToNormalModeAsync(CancellationToken) method instead.")]
    public async ValueTask TurnIntoRegularNodeAsync(CancellationToken token)
        => await RevertToNormalModeAsync(token).ConfigureAwait(false);

    /// <inheritdoc cref="IStandbyModeSupport.RevertToNormalModeAsync(CancellationToken)"/>
    public async ValueTask<bool> RevertToNormalModeAsync(CancellationToken token = default)
    {
        ThrowIfDisposed();

        if (TryGetLocalMember() is not null && state is StandbyState<TMember> { Resumable: true } standbyState)
        {
            var tokenSource = token.LinkTo(LifecycleToken);
            var transitionLock = default(AsyncLock.Holder);
            try
            {
                transitionLock = await transitionSync.AcquireAsync(token).ConfigureAwait(false);

                // ensure that we trying to update the same state
                if (TryGetLocalMember() is not null && ReferenceEquals(state, standbyState))
                {
                    var newState = new FollowerState<TMember>(this) { Metrics = Metrics };
                    await UpdateStateAsync(newState).ConfigureAwait(false);
                    newState.StartServing(ElectionTimeout, LifecycleToken);
                    return true;
                }
            }
            catch (OperationCanceledException e) when (tokenSource is not null)
            {
                throw new OperationCanceledException(e.Message, e, tokenSource.CancellationOrigin);
            }
            finally
            {
                tokenSource?.Dispose();
                transitionLock.Dispose();
            }
        }

        return false;
    }

    /// <inheritdoc cref="IStandbyModeSupport.EnableStandbyModeAsync(CancellationToken)"/>
    public async ValueTask<bool> EnableStandbyModeAsync(CancellationToken token = default)
    {
        ThrowIfDisposed();

        RaftState<TMember> currentState;
        if ((currentState = state) is FollowerState<TMember> or CandidateState<TMember>)
        {
            var tokenSource = token.LinkTo(LifecycleToken);
            var transitionLock = default(AsyncLock.Holder);
            try
            {
                transitionLock = await transitionSync.AcquireAsync(token).ConfigureAwait(false);

                // ensure that we trying to update the same state
                if (ReferenceEquals(state, currentState))
                {
                    await UpdateStateAsync(new StandbyState<TMember>(this)).ConfigureAwait(false);
                    return true;
                }
            }
            catch (OperationCanceledException e) when (tokenSource is not null)
            {
                throw new OperationCanceledException(e.Message, e, tokenSource.CancellationOrigin);
            }
            finally
            {
                tokenSource?.Dispose();
                transitionLock.Dispose();
            }
        }

        return false;
    }

    /// <inheritdoc cref="IStandbyModeSupport.Standby"/>
    public bool Standby => state is StandbyState<TMember>;

    private async Task CancelPendingRequestsAsync()
    {
        var tasks = members.Values.Select(static m => m.CancelPendingRequestsAsync().AsTask()).ToArray();

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Logger.FailedToCancelPendingRequests(e);
        }
        finally
        {
            Array.Clear(tasks); // help GC
        }
    }

    private ValueTask UpdateStateAsync(RaftState<TMember> newState)
        => Interlocked.Exchange(ref state, newState).DisposeAsync();

    /// <summary>
    /// Stops serving local member.
    /// </summary>
    /// <param name="token">The token that can be used to cancel shutdown process.</param>
    /// <returns>The task representing asynchronous execution of the method.</returns>
    public virtual Task StopAsync(CancellationToken token)
    {
        return LifecycleToken.IsCancellationRequested ? Task.CompletedTask : StopAsync();

        async Task StopAsync()
        {
            transitionCancellation.Cancel(false);
            await CancelPendingRequestsAsync().ConfigureAwait(false);
            electionEvent.TrySetCanceled();
            LocalMemberGone();
            using (await transitionSync.AcquireAsync(token).ConfigureAwait(false))
            {
                await MoveToStandbyState().ConfigureAwait(false);
            }
        }

        void LocalMemberGone()
        {
            var localMember = TryGetLocalMember();
            if (localMember is not null)
                OnMemberRemoved(localMember);
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

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask StepDown()
    {
        Logger.DowngradingToFollowerState(Term);
        switch (state)
        {
            case FollowerState<TMember> followerState:
                followerState.Refresh();
                break;
            case LeaderState<TMember> or CandidateState<TMember>:
                var newState = new FollowerState<TMember>(this) { Metrics = Metrics };
                await UpdateStateAsync(newState).ConfigureAwait(false);
                newState.StartServing(ElectionTimeout, LifecycleToken);
                Metrics?.MovedToFollowerState();
                break;
        }

        Logger.DowngradedToFollowerState(Term);
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
    protected async ValueTask<Result<bool>> InstallSnapshotAsync<TSnapshot>(ClusterMemberId sender, long senderTerm, TSnapshot snapshot, long snapshotIndex, CancellationToken token)
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
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))] // hot path, avoid allocations
    protected async ValueTask<Result<bool>> AppendEntriesAsync<TEntry>(ClusterMemberId sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, IClusterConfiguration config, bool applyConfig, CancellationToken token)
        where TEntry : notnull, IRaftLogEntry
    {
        var result = false;
        using var tokenSource = token.LinkTo(LifecycleToken);
        using var transitionLock = await transitionSync.AcquireAsync(token).ConfigureAwait(false);
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
                using (new FollowerState<TMember>.TransitionSuppressionScope(state as FollowerState<TMember>))
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

                    // process configuration
                    var fingerprint = (ConfigurationStorage.ProposedConfiguration ?? ConfigurationStorage.ActiveConfiguration).Fingerprint;
                    Logger.IncomingConfiguration(fingerprint, config.Fingerprint, applyConfig);
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

                // This node is in sync with the leader and no entries arrived
                if (emptySet && senderMember is not null)
                {
                    replicationHandlers.Invoke(this, senderMember);
                    await UnfreezeAsync().ConfigureAwait(false);
                }
            }
        }

        return new(currentTerm, result);
    }

    /// <summary>
    /// Receives preliminary vote from the potential Candidate in the cluster.
    /// </summary>
    /// <param name="sender">The sender of the replica message.</param>
    /// <param name="nextTerm">Caller's current term + 1.</param>
    /// <param name="lastLogIndex">Index of candidate's last log entry.</param>
    /// <param name="lastLogTerm">Term of candidate's last log entry.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>Pre-vote result received from the member.</returns>
    protected async ValueTask<Result<PreVoteResult>> PreVoteAsync(ClusterMemberId sender, long nextTerm, long lastLogIndex, long lastLogTerm, CancellationToken token)
    {
        PreVoteResult result;
        long currentTerm;

        // PreVote doesn't cause transition to another Raft state so locking not needed
        using (var tokenSource = token.LinkTo(LifecycleToken))
        {
            currentTerm = auditTrail.Term;

            // provide leader stickiness
            if (aggressiveStickiness && state is LeaderState<TMember>)
            {
                result = PreVoteResult.RejectedByLeader;
            }
            else if (members.ContainsKey(sender) && Timestamp.VolatileRead(ref lastUpdated).Elapsed >= ElectionTimeout && currentTerm <= nextTerm && await auditTrail.IsUpToDateAsync(lastLogIndex, lastLogTerm, token).ConfigureAwait(false))
            {
                result = PreVoteResult.Accepted;
            }
            else
            {
                result = PreVoteResult.RejectedByFollower;
            }
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
        await foreach (var response in SendRequestsAsync(members.Values, currentTerm, lastIndex, lastTerm, LifecycleToken).ConfigureAwait(false))
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

        static IAsyncEnumerable<Task<Result<PreVoteResult>>> SendRequestsAsync(IEnumerable<TMember> members, long currentTerm, long lastIndex, long lastTerm, CancellationToken token)
        {
            var responses = new TaskCompletionPipe<Task<Result<PreVoteResult>>>();
            foreach (var member in members)
                responses.Add(member.PreVoteAsync(currentTerm, lastIndex, lastTerm, token));

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
    protected async ValueTask<Result<bool>> VoteAsync(ClusterMemberId sender, long senderTerm, long lastLogIndex, long lastLogTerm, CancellationToken token)
    {
        var result = false;
        var currentTerm = auditTrail.Term;

        // provide leader stickiness
        if (currentTerm > senderTerm || Timestamp.VolatileRead(ref lastUpdated).Elapsed < ElectionTimeout || !members.ContainsKey(sender))
            goto exit;

        var tokenSource = token.LinkTo(LifecycleToken);
        var transitionLock = default(AsyncLock.Holder);
        try
        {
            transitionLock = await transitionSync.AcquireAsync(token).ConfigureAwait(false);
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
            else if (state is FollowerState<TMember> follower)
            {
                follower.Refresh();
            }
            else if (state is StandbyState<TMember>)
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
        finally
        {
            tokenSource?.Dispose();
            transitionLock.Dispose();
        }

    exit:
        return new(currentTerm, result);
    }

    /// <summary>
    /// Revokes leadership of the local node.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/>, if leadership is revoked successfully; otherwise, <see langword="false"/>.</returns>
    protected async ValueTask<bool> ResignAsync(CancellationToken token)
    {
        if (state is LeaderState<TMember> leaderState)
        {
            var tokenSource = token.LinkTo(LifecycleToken);
            var lockHolder = default(AsyncLock.Holder);
            try
            {
                lockHolder = await transitionSync.AcquireAsync(token).ConfigureAwait(false);

                if (ReferenceEquals(state, leaderState))
                {
                    var newState = new FollowerState<TMember>(this) { Metrics = Metrics };
                    await UpdateStateAsync(newState).ConfigureAwait(false);
                    Leader = null;
                    newState.StartServing(ElectionTimeout, LifecycleToken);
                    return true;
                }
            }
            catch (OperationCanceledException e) when (tokenSource is not null)
            {
                throw new OperationCanceledException(e.Message, e, tokenSource.CancellationOrigin);
            }
            finally
            {
                tokenSource?.Dispose();
                lockHolder.Dispose();
            }
        }

        return false;
    }

    /// <summary>
    /// Processes <see cref="IRaftClusterMember.SynchronizeAsync(long, CancellationToken)"/>
    /// request.
    /// </summary>
    /// <param name="commitIndex">The index of the last committed log entry on the sender side.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The index of the last committed log entry known by the leader.</returns>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))] // hot path, avoid allocations
    protected async ValueTask<long?> SynchronizeAsync(long commitIndex, CancellationToken token)
    {
        // do not execute the next round of heartbeats if the sender is already in sync with the leader
        if (state is LeaderState<TMember> leaderState)
        {
            if (commitIndex < auditTrail.LastCommittedEntryIndex)
            {
                try
                {
                    await leaderState.ForceReplicationAsync(token).ConfigureAwait(false);
                }
                catch (InvalidOperationException)
                {
                    // local node is not a leader
                    return null;
                }
            }

            return auditTrail.LastCommittedEntryIndex;
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
            if (state is LeaderState<TMember> leaderState)
            {
                await leaderState.ForceReplicationAsync(token).ConfigureAwait(false);
            }
            else if (Leader is TMember leader)
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
        return await ResignAsync(token).ConfigureAwait(false) ||
            (Leader is TMember leader && await leader.ResignAsync(token).ConfigureAwait(false));
    }

    private ValueTask MoveToStandbyState(bool resumable = true)
    {
        Leader = null;
        return UpdateStateAsync(new StandbyState<TMember>(this) { Resumable = resumable });
    }

    /// <inheritdoc />
    async void IRaftStateMachine<TMember>.MoveToFollowerState(IRaftStateMachine.IWeakCallerStateIdentity callerState, bool randomizeTimeout, long? newTerm)
    {
        var lockHolder = default(AsyncLock.Holder);
        try
        {
            lockHolder = await transitionSync.TryAcquireAsync(LifecycleToken).SuppressDisposedStateOrCancellation().ConfigureAwait(false);
            if (lockHolder && callerState.IsValid(state))
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
            callerState.Clear();
        }
    }

    /// <inheritdoc />
    async void IRaftStateMachine<TMember>.MoveToCandidateState(IRaftStateMachine.IWeakCallerStateIdentity callerState)
    {
        var lockHolder = default(AsyncLock.Holder);
        try
        {
            var currentTerm = auditTrail.Term;
            var readyForTransition = await PreVoteAsync(currentTerm).ConfigureAwait(false);
            lockHolder = await transitionSync.TryAcquireAsync(LifecycleToken).SuppressDisposedStateOrCancellation().ConfigureAwait(false);
            if (lockHolder && state is FollowerState<TMember> { IsExpired: true } followerState && callerState.IsValid(followerState))
            {
                Logger.TransitionToCandidateStateStarted(Term);

                // if term changed after lock then assumes that leader will be updated soon
                if (currentTerm == auditTrail.Term)
                    Leader = null;
                else
                    readyForTransition = false;

                if (readyForTransition)
                {
                    var newState = new CandidateState<TMember>(this, await auditTrail.IncrementTermAsync(localMemberId).ConfigureAwait(false));
                    await UpdateStateAsync(newState).ConfigureAwait(false);

                    // vote for self
                    newState.StartVoting(electionTimeout, auditTrail);
                    Metrics?.MovedToCandidateState();
                    Logger.TransitionToCandidateStateCompleted(Term);
                }
                else
                {
                    // resume follower state
                    followerState.StartServing(ElectionTimeout, LifecycleToken);
                    Logger.DowngradedToFollowerState(Term);
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
            callerState.Clear();
        }
    }

    /// <inheritdoc />
    async void IRaftStateMachine<TMember>.MoveToLeaderState(IRaftStateMachine.IWeakCallerStateIdentity callerState, TMember newLeader)
    {
        var lockHolder = default(AsyncLock.Holder);

        try
        {
            Logger.TransitionToLeaderStateStarted(Term);
            lockHolder = await transitionSync.TryAcquireAsync(LifecycleToken).SuppressDisposedStateOrCancellation().ConfigureAwait(false);
            long currentTerm;
            if (lockHolder && state is CandidateState<TMember> candidateState && callerState.IsValid(candidateState) && candidateState.Term == (currentTerm = auditTrail.Term))
            {
                var newState = new LeaderState<TMember>(this, allowPartitioning, currentTerm, LeaderLeaseDuration)
                {
                    Metrics = Metrics,
                    FailureDetectorFactory = FailureDetectorFactory,
                };

                await UpdateStateAsync(newState).ConfigureAwait(false);

                Leader = newLeader;
                await auditTrail.AppendNoOpEntry(LifecycleToken).ConfigureAwait(false);
                newState.StartLeading(HeartbeatTimeout, auditTrail, ConfigurationStorage, LifecycleToken);

                Metrics?.MovedToLeaderState();
                Logger.TransitionToLeaderStateCompleted(Term);
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
            callerState.Clear();
        }
    }

    /// <summary>
    /// Notifies that the member is unavailable.
    /// </summary>
    /// <remarks>
    /// It's an infrastructure method that can be used to remove unavailable member from the cluster configuration
    /// at the leader side.
    /// </remarks>
    /// <param name="member">The member that is considered as unavailable.</param>
    /// <param name="token">The token associated with <see cref="LeadershipToken"/> that identifies the leader state at the time of detection.</param>
    /// <returns>The task representing asynchronous result.</returns>
    protected virtual ValueTask UnavailableMemberDetected(TMember member, CancellationToken token)
        => token.IsCancellationRequested ? ValueTask.FromCanceled(token) : ValueTask.CompletedTask;

    /// <inheritdoc />
    async void IRaftStateMachine<TMember>.UnavailableMemberDetected(IRaftStateMachine.IWeakCallerStateIdentity callerState, TMember member, CancellationToken token)
    {
        // check state to drop old notifications (double-check pattern)
        if (callerState.IsValid(state) && membershipState.FalseToTrue())
        {
            try
            {
                Logger.UnresponsiveMemberDetected(member.EndPoint);
                await UnavailableMemberDetected(member, token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Logger.FailedToProcessUnresponsiveMember(member.EndPoint, e);
            }
            finally
            {
                membershipState.Value = false;
            }
        }

        callerState.Clear();
    }

    /// <summary>
    /// Forces replication.
    /// </summary>
    /// <param name="token">The token that can be used to cancel waiting.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="InvalidOperationException">The local cluster member is not a leader.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public Task ForceReplicationAsync(CancellationToken token = default)
        => state is LeaderState<TMember> leaderState
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

        var tokenSource = token.LinkTo(LifecycleToken);
        try
        {
            // 1 - append entry to the log
            var index = await auditTrail.AppendAsync(entry, token).ConfigureAwait(false);

            // 2 - force replication
            await ForceReplicationAsync(token).ConfigureAwait(false);

            // 3 - wait for commit
            await auditTrail.WaitForCommitAsync(index, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException e) when (tokenSource is not null)
        {
            throw new OperationCanceledException(e.Message, e, tokenSource.CancellationOrigin);
        }
        finally
        {
            tokenSource?.Dispose();
        }

        return auditTrail.Term == entry.Term;
    }

    private TMember? TryGetPeer(EndPoint peer)
    {
        foreach (var member in members.Values)
        {
            if (EndPointComparer.Equals(member.EndPoint, peer))
                return member;
        }

        return null;
    }

    /// <inheritdoc />
    IRaftClusterMember? IPeerMesh<IRaftClusterMember>.TryGetPeer(EndPoint peer) => TryGetPeer(peer);

    /// <inheritdoc />
    IClusterMember? IPeerMesh<IClusterMember>.TryGetPeer(EndPoint peer) => TryGetPeer(peer);

    /// <inheritdoc />
    IReadOnlySet<EndPoint> IPeerMesh.Peers => ImmutableHashSet.CreateRange(EndPointComparer, members.Values.Select(static m => m.EndPoint));

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (!LifecycleToken.IsCancellationRequested)
                Logger.StopAsyncWasNotCalled();

            Dispose(Interlocked.Exchange(ref members, MemberList.Empty));
            transitionCancellation.Dispose();
            transitionSync.Dispose();
            state.Dispose();
            TrySetDisposedException(readinessProbe);
            ConfigurationStorage.Dispose();

            memberAddedHandlers = memberRemovedHandlers = default;
            leaderChangedHandlers = default;
            TrySetDisposedException(electionEvent);
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        Dispose(disposing: true);
    }

    /// <inheritdoc />
    public new ValueTask DisposeAsync() => base.DisposeAsync();
}
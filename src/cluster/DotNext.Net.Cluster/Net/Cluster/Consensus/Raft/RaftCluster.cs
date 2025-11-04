using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Collections.Specialized;
using Diagnostics;
using Extensions;
using IO.Log;
using Membership;
using Replication;
using Threading;
using Threading.Tasks;

/// <summary>
/// Represents transport-independent implementation of Raft protocol.
/// </summary>
/// <typeparam name="TMember">The type implementing communication details with remote nodes.</typeparam>
public abstract partial class RaftCluster<TMember> : Disposable, IUnresponsiveClusterMemberRemovalSupport, IStandbyModeSupport, IRaftStateMachine<TMember>, IAsyncDisposable
    where TMember : class, IRaftClusterMember, IDisposable
{
    private readonly bool aggressiveStickiness;
    private readonly ElectionTimeout electionTimeoutProvider;
    private readonly CancellationTokenSource transitionCancellation;
    private readonly double heartbeatThreshold, clockDriftBound;
    private readonly Random random;
    private readonly TaskCompletionSource readinessProbe;
    private readonly bool standbyNode;
    private readonly AsyncExclusiveLock transitionLock; // used to synchronize state transitions
    private readonly TagList measurementTags;
    private readonly CancellationTokenMultiplexer cancellationTokens;

    private volatile RaftState<TMember> state;
    private volatile TaskCompletionSource<TMember> electionEvent;
    private volatile TaskCompletionSource<CancellationToken> leadershipEvent;
    private InvocationList<Action<RaftCluster<TMember>, TMember?>> leaderChangedHandlers;
    private InvocationList<Action<RaftCluster<TMember>, TMember>> replicationHandlers;
    private volatile int electionTimeout;
    private IPersistentState auditTrail;
    private Timestamp lastUpdated; // volatile
    private bool configurationReplicated;

    /// <summary>
    /// Initializes a new cluster manager for the local node.
    /// </summary>
    /// <param name="config">The configuration of the local node.</param>
    protected RaftCluster(IClusterMemberConfiguration config)
        : this(config, default)
    {
    }

    /// <summary>
    /// Initializes a new cluster manager for the local node.
    /// </summary>
    /// <param name="config">The configuration of the local node.</param>
    /// <param name="measurementTags">A tags to be attached to each performance measurement.</param>
    [CLSCompliant(false)]
    protected RaftCluster(IClusterMemberConfiguration config, in TagList measurementTags)
    {
        ArgumentNullException.ThrowIfNull(config);

        electionTimeoutProvider = config.ElectionTimeout;
        random = new();
        electionTimeout = electionTimeoutProvider.RandomTimeout(random);
        members = IMemberList.Empty;
        transitionLock = new() { MeasurementTags = measurementTags };
        transitionCancellation = new();
        LifecycleToken = transitionCancellation.Token;
        auditTrail = new ConsensusOnlyState();
        heartbeatThreshold = config.HeartbeatThreshold;
        standbyNode = config.Standby;
        clockDriftBound = config.ClockDriftBound;
        readinessProbe = new(TaskCreationOptions.RunContinuationsAsynchronously);
        aggressiveStickiness = config.AggressiveLeaderStickiness;
        electionEvent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        leadershipEvent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        state = new StandbyState<TMember>(this, TimeSpan.FromMilliseconds(electionTimeout));
        EndPointComparer = config.EndPointComparer;
        this.measurementTags = measurementTags;
        cancellationTokens = new();
    }

    /// <summary>
    /// Combines multiple cancellation tokens.
    /// </summary>
    /// <param name="tokens">The tokens to be combined.</param>
    /// <returns>The lifetime of the combined token.</returns>
    protected CancellationTokenMultiplexer.Scope CombineTokens(ReadOnlySpan<CancellationToken> tokens)
        => cancellationTokens.Combine(tokens);

    /// <summary>
    /// Gets or sets failure detector to be used by the leader node to detect and remove unresponsive followers.
    /// </summary>
    public Func<TimeSpan, TMember, IFailureDetector>? FailureDetectorFactory
    {
        get;
        init;
    }

    /// <inheritdoc/>
    Func<TimeSpan, IRaftClusterMember, IFailureDetector>? IUnresponsiveClusterMemberRemovalSupport.FailureDetectorFactory
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

    /// <inheritdoc />
    ILogger IRaftStateMachine.Logger => Logger;

    /// <inheritdoc />
    void IRaftStateMachine.UpdateLeaderStickiness() => Timestamp.Refresh(ref lastUpdated);

    /// <inheritdoc />
    ref readonly TagList IRaftStateMachine.MeasurementTags => ref measurementTags;

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

    /// <inheritdoc cref="IRaftCluster.TryGetLeaseToken(out CancellationToken)"/>
    public bool TryGetLeaseToken(out CancellationToken token)
    {
        if (state is LeaderState<TMember> leader)
            return leader.TryGetLeaseToken(out token);

        token = new(canceled: true);
        return false;
    }

    /// <inheritdoc cref="IRaftCluster.LeadershipToken"/>
    public CancellationToken LeadershipToken => (state as LeaderState<TMember>)?.Token ?? new(canceled: true);

    /// <inheritdoc cref="IRaftCluster.ConsensusToken"/>
    public CancellationToken ConsensusToken => (state as ConsensusState<TMember>)?.Token ?? new(canceled: true);

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private LeaderState<TMember> LeaderStateOrException
        => state as LeaderState<TMember> ?? throw new NotLeaderException();

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
    public IReadOnlyCollection<TMember> Members => members.Values;

    /// <inheritdoc />
    IReadOnlyCollection<IRaftClusterMember> IRaftCluster.Members => Members;

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

    /// <inheritdoc cref="ICluster.WaitForLeaderAsync(TimeSpan, CancellationToken)"/>
    public Task<TMember> WaitForLeaderAsync(TimeSpan timeout, CancellationToken token = default)
        => electionEvent.Task.WaitAsync(timeout, token);

    /// <inheritdoc />
    ValueTask<IClusterMember> ICluster.WaitForLeaderAsync(TimeSpan timeout, CancellationToken token)
        => new(WaitForLeaderAsync(timeout, token).Convert<TMember, IClusterMember>());

    /// <inheritdoc cref="IRaftCluster.WaitForLeadershipAsync(TimeSpan, CancellationToken)"/>
    public ValueTask<CancellationToken> WaitForLeadershipAsync(TimeSpan timeout, CancellationToken token = default)
        => new(leadershipEvent.Task.WaitAsync(timeout, token));

    private ValueTask UnfreezeAsync()
    {
        ValueTask result;

        // ensure that local member has been received
        if (readinessProbe.Task.IsCompleted || TryGetLocalMember() is null)
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
            var newState = new FollowerState<TMember>(this, consensusReached: true);
            await UpdateStateAsync(newState).ConfigureAwait(false);
            newState.StartServing(ElectionTimeout);
            readinessProbe.TrySetResult();
        }
    }

    private TMember? TryGetLocalMember() => members.Values.FirstOrDefault(static m => m.IsRemote is false);

    /// <summary>
    /// Determines whether the specified candidate represents a local node.
    /// </summary>
    /// <param name="candidate">The candidate to check.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if <paramref name="candidate"/> represents a local node; otherwise, <see langword="false"/>.</returns>
    protected abstract ValueTask<bool> DetectLocalMemberAsync(TMember candidate, CancellationToken token);

    /// <summary>
    /// Starts serving local member.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the initialization process.</param>
    /// <returns>The task representing asynchronous execution of the method.</returns>
    /// <seealso cref="StartFollowing"/>
    public virtual async Task StartAsync(CancellationToken token)
    {
        await auditTrail.InitializeAsync(token).ConfigureAwait(false);

        // local member is known then turn readiness probe into signalled state and start serving the messages from the cluster
        foreach (var member in members.Values)
        {
            if (await DetectLocalMemberAsync(member, token).ConfigureAwait(false))
            {
                state = standbyNode ? new StandbyState<TMember>(this, LeaderLeaseDuration) : new FollowerState<TMember>(this, consensusReached: false);
                readinessProbe.TrySetResult();
                Logger.StartedAsFollower(member.EndPoint);
                return;
            }
        }

        // local member is not known. Start in frozen state and wait when the current node will be added to the cluster
        state = new StandbyState<TMember>(this, LeaderLeaseDuration);
        Logger.StartedAsFrozen();
    }

    /// <summary>
    /// Starts Follower timer.
    /// </summary>
    protected void StartFollowing() => (state as FollowerState<TMember>)?.StartServing(ElectionTimeout);

    /// <inheritdoc cref="IStandbyModeSupport.RevertToNormalModeAsync(CancellationToken)"/>
    public async ValueTask<bool> RevertToNormalModeAsync(CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (TryGetLocalMember() is not null && state is StandbyState<TMember> { Resumable: true } standbyState)
        {
            var tokenSource = CombineTokens([token, LifecycleToken]);
            var lockTaken = false;
            try
            {
                await transitionLock.AcquireAsync(tokenSource.Token).ConfigureAwait(false);
                lockTaken = true;

                // ensure that we're trying to update the same state
                if (TryGetLocalMember() is not null && ReferenceEquals(state, standbyState))
                {
                    var newState = new FollowerState<TMember>(this, consensusReached: standbyState.Token is { IsCancellationRequested: false });
                    await UpdateStateAsync(newState).ConfigureAwait(false);
                    newState.StartServing(ElectionTimeout);
                    return true;
                }
            }
            catch (OperationCanceledException e) when (tokenSource.Token == e.CancellationToken)
            {
                throw new OperationCanceledException(e.Message, e, tokenSource.CancellationOrigin);
            }
            finally
            {
                if (lockTaken)
                    transitionLock.Release();

                await tokenSource.DisposeAsync().ConfigureAwait(false);
            }
        }

        return false;
    }

    /// <inheritdoc cref="IStandbyModeSupport.EnableStandbyModeAsync(CancellationToken)"/>
    public async ValueTask<bool> EnableStandbyModeAsync(CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        RaftState<TMember> currentState;
        if ((currentState = state) is not StandbyState<TMember>)
        {
            var tokenSource = CombineTokens([token, LifecycleToken]);
            var lockTaken = false;
            try
            {
                await transitionLock.AcquireAsync(tokenSource.Token).ConfigureAwait(false);
                lockTaken = true;

                // ensure that we're trying to update the same state
                if (ReferenceEquals(state, currentState))
                {
                    await UpdateStateAsync(new StandbyState<TMember>(this, LeaderLeaseDuration)).ConfigureAwait(false);
                    return true;
                }
            }
            catch (OperationCanceledException e) when (tokenSource.Token == e.CancellationToken)
            {
                throw new OperationCanceledException(e.Message, e, tokenSource.CancellationOrigin);
            }
            finally
            {
                if (lockTaken)
                    transitionLock.Release();
                
                await tokenSource.DisposeAsync().ConfigureAwait(false);
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
    {
        if (leadershipEvent is { Task.IsCompletedSuccessfully: true } leadershipEventCopy)
        {
            Interlocked.CompareExchange(ref leadershipEvent, new(TaskCreationOptions.RunContinuationsAsynchronously), leadershipEventCopy);
        }

        return Interlocked.Exchange(ref state, newState).DisposeAsync();
    }

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
            electionEvent.TrySetCanceled(LifecycleToken);
            LocalMemberGone();
            var lockTaken = false;
            try
            {
                await transitionLock.AcquireAsync(token).ConfigureAwait(false);
                lockTaken = true;

                await MoveToStandbyState().ConfigureAwait(false);
            }
            finally
            {
                if (lockTaken)
                    transitionLock.Release();
            }
        }

        void LocalMemberGone()
        {
            if (TryGetLocalMember() is { } localMember)
                OnMemberRemoved(localMember);
        }
    }

    private ValueTask StepDownAsync(long newTerm, bool consensusReached)
        => newTerm > auditTrail.Term ? UpdateTermAndStepDownAsync(newTerm, consensusReached) : StepDownAsync(consensusReached);

    private async ValueTask UpdateTermAndStepDownAsync(long newTerm, bool consensusReached)
    {
        await auditTrail.UpdateTermAsync(newTerm, resetLastVote: true, LifecycleToken).ConfigureAwait(false);
        await StepDownAsync(consensusReached).ConfigureAwait(false);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask StepDownAsync(bool consensusReached)
    {
        Logger.DowngradingToFollowerState(Term);
        switch (state)
        {
            case RefreshableState<TMember> followerOrStandbyState:
                followerOrStandbyState.Refresh();
                break;
            case LeaderState<TMember> or CandidateState<TMember>:
                var newState = new FollowerState<TMember>(this, consensusReached);
                await UpdateStateAsync(newState).ConfigureAwait(false);
                newState.StartServing(ElectionTimeout);
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
    /// <returns><see langword="true"/> if snapshot is installed successfully; <see langword="null"/> if snapshot is outdated.</returns>
    protected async ValueTask<Result<HeartbeatResult>> InstallSnapshotAsync<TSnapshot>(ClusterMemberId sender, long senderTerm, TSnapshot snapshot, long snapshotIndex, CancellationToken token)
        where TSnapshot : IRaftLogEntry
    {
        Result<HeartbeatResult> result;
        var lockTaken = false;
        var tokenSource = CombineTokens([token, LifecycleToken]);
        try
        {
            await transitionLock.AcquireAsync(tokenSource.Token).ConfigureAwait(false);
            lockTaken = true;

            result = new() { Term = Term };
            if (snapshot.IsSnapshot && senderTerm >= result.Term && snapshotIndex > auditTrail.LastCommittedEntryIndex)
            {
                Timestamp.Refresh(ref lastUpdated);
                await StepDownAsync(senderTerm, consensusReached: true).ConfigureAwait(false);
                Leader = TryGetMember(sender);
                await auditTrail.AppendAsync(snapshot, snapshotIndex, tokenSource.Token).ConfigureAwait(false);
                result = result with
                {
                    Value = senderTerm == snapshot.Term ? HeartbeatResult.ReplicatedWithLeaderTerm : HeartbeatResult.Replicated
                };
            }
        }
        catch (OperationCanceledException e) when (e.CancellationToken == tokenSource.Token)
        {
            throw new OperationCanceledException(e.Message, e, tokenSource.CancellationOrigin);
        }
        finally
        {
            if (lockTaken)
                transitionLock.Release();

            await tokenSource.DisposeAsync().ConfigureAwait(false);
        }

        return result;
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
    /// <returns>The processing result.</returns>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))] // hot path, avoid allocations
    protected async ValueTask<Result<HeartbeatResult>> AppendEntriesAsync<TEntry>(ClusterMemberId sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, IClusterConfiguration config, bool applyConfig, CancellationToken token)
        where TEntry : IRaftLogEntry
    {
        Result<HeartbeatResult> result;
        var lockTaken = false;
        var tokenSource = CombineTokens([token, LifecycleToken]);
        try
        {
            await transitionLock.AcquireAsync(tokenSource.Token).ConfigureAwait(false);
            lockTaken = true;

            result = new() { Term = Term };
            if (result.Term <= senderTerm)
            {
                Timestamp.Refresh(ref lastUpdated);
                await StepDownAsync(senderTerm, consensusReached: true).ConfigureAwait(false);
                var senderMember = TryGetMember(sender);
                Leader = senderMember;
                if (await auditTrail.ContainsAsync(prevLogIndex, prevLogTerm, tokenSource.Token).ConfigureAwait(false))
                {
                    bool emptySet;

                    if (entries.RemainingCount is 0L)
                    {
                        emptySet = true;
                    }
                    else
                    {
                        entries = new ReplicationWithSenderTermDetector<TEntry>(entries, senderTerm);
                        emptySet = false;
                    }

                    // prevent Follower state transition during processing of received log entries
                    using (new RefreshableState<TMember>.TransitionSuppressionScope(state as RefreshableState<TMember>))
                    {
                        /*
                         * AppendAsync is called with skipCommitted=true because HTTP response from the previous
                         * replication might fail but the log entry was committed by the local node.
                         * In this case the leader repeat its replication from the same prevLogIndex which is already committed locally.
                         * skipCommitted=true allows to skip the passed committed entry and append uncommitted entries.
                         * If it is 'false' then the method will throw the exception and the node becomes unavailable in each replication cycle.
                         */
                        await auditTrail.AppendAndCommitAsync(entries, prevLogIndex + 1L, true, commitIndex, tokenSource.Token).ConfigureAwait(false);
                        result = result with
                        {
                            Value = entries is ReplicationWithSenderTermDetector<TEntry> { IsReplicatedWithExpectedTerm: true }
                                ? HeartbeatResult.ReplicatedWithLeaderTerm
                                : HeartbeatResult.Replicated
                        };

                        // process configuration
                        var fingerprint = (ConfigurationStorage.ProposedConfiguration ?? ConfigurationStorage.ActiveConfiguration).Fingerprint;
                        Logger.IncomingConfiguration(fingerprint, config.Fingerprint, applyConfig);
                        switch ((config.Fingerprint == fingerprint, applyConfig))
                        {
                            case (true, true):
                                // Perf: avoid calling ApplyAsync if configuration remains unchanged
                                if (!configurationReplicated)
                                {
                                    await ConfigurationStorage.ApplyAsync(tokenSource.Token).ConfigureAwait(false);
                                    configurationReplicated = true;
                                }

                                break;
                            case (false, false):
                                await ConfigurationStorage.ProposeAsync(config, tokenSource.Token).ConfigureAwait(false);
                                goto default;
                            case (false, true):
                                result = result with { Value = HeartbeatResult.Rejected };
                                goto default;
                            default:
                                configurationReplicated = false;
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
        }
        catch (OperationCanceledException e) when (e.CancellationToken == tokenSource.Token)
        {
            throw new OperationCanceledException(e.Message, e, tokenSource.CancellationOrigin);
        }
        finally
        {
            if (lockTaken)
                transitionLock.Release();

            await tokenSource.DisposeAsync().ConfigureAwait(false);
        }

        return result;
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
        Result<PreVoteResult> result;

        // PreVote doesn't cause transition to another Raft state so locking not needed
        var tokenSource = CombineTokens([token, LifecycleToken]);
        try
        {
            result = new() { Term = Term };

            // provide leader stickiness
            if (aggressiveStickiness && state is LeaderState<TMember>)
            {
                result = result with { Value = PreVoteResult.RejectedByLeader };
            }
            else if (members.ContainsKey(sender) && Timestamp.VolatileRead(ref lastUpdated).Elapsed >= ElectionTimeout && result.Term <= nextTerm &&
                     await auditTrail.IsUpToDateAsync(lastLogIndex, lastLogTerm, tokenSource.Token).ConfigureAwait(false))
            {
                result = result with { Value = PreVoteResult.Accepted };
            }
        }
        catch (OperationCanceledException e) when (e.CancellationToken == tokenSource.Token)
        {
            throw new OperationCanceledException(e.Message, e, tokenSource.CancellationOrigin);
        }
        finally
        {
            await tokenSource.DisposeAsync().ConfigureAwait(false);
        }

        return result;
    }

    // pre-vote logic that allow to decide about transition to candidate state
    private async Task<bool> PreVoteAsync(long currentTerm)
    {
        var lastIndex = auditTrail.LastEntryIndex;
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
        var result = new Result<bool> { Term = Term };

        // provide leader stickiness
        if (result.Term > senderTerm || Timestamp.VolatileRead(ref lastUpdated).Elapsed < ElectionTimeout || !members.ContainsKey(sender))
            goto exit;

        var tokenSource = CombineTokens([token, LifecycleToken]);
        var lockTaken = false;
        try
        {
            await transitionLock.AcquireAsync(tokenSource.Token).ConfigureAwait(false);
            lockTaken = true;

            result = result with { Term = Term };

            if (result.Term > senderTerm)
            {
                goto exit;
            }
            else if (result.Term != senderTerm)
            {
                Leader = null;
                await StepDownAsync(senderTerm, consensusReached: false).ConfigureAwait(false);
            }
            else if (state is RefreshableState<TMember> followerOrStandbyState)
            {
                followerOrStandbyState.Refresh();
            }
            else
            {
                goto exit;
            }

            if (auditTrail.IsVotedFor(sender) && await auditTrail.IsUpToDateAsync(lastLogIndex, lastLogTerm, tokenSource.Token).ConfigureAwait(false))
            {
                await auditTrail.UpdateVotedForAsync(sender, tokenSource.Token).ConfigureAwait(false);
                result = result with { Value = true };
            }
        }
        catch (OperationCanceledException e) when (e.CancellationToken == tokenSource.Token)
        {
            throw new OperationCanceledException(e.Message, e, tokenSource.CancellationOrigin);
        }
        finally
        {
            if (lockTaken)
                transitionLock.Release();

            await tokenSource.DisposeAsync().ConfigureAwait(false);
        }

    exit:
        return result;
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
            var tokenSource = CombineTokens([token, LifecycleToken]);
            var lockTaken = false;
            try
            {
                await transitionLock.AcquireAsync(tokenSource.Token).ConfigureAwait(false);
                lockTaken = true;

                if (ReferenceEquals(state, leaderState))
                {
                    var newState = new FollowerState<TMember>(this, consensusReached: false);
                    await UpdateStateAsync(newState).ConfigureAwait(false);
                    Leader = null;
                    newState.StartServing(ElectionTimeout);
                    return true;
                }
            }
            catch (OperationCanceledException e) when (tokenSource.Token == e.CancellationToken)
            {
                throw new OperationCanceledException(e.Message, e, tokenSource.CancellationOrigin);
            }
            finally
            {
                if (lockTaken)
                    transitionLock.Release();

                await tokenSource.DisposeAsync().ConfigureAwait(false);
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
    protected ValueTask<long?> SynchronizeAsync(long commitIndex, CancellationToken token)
    {
        long? result = null;

        // do not execute the next round of heartbeats if the sender is already in sync with the leader
        if (state is LeaderState<TMember> leaderState)
        {
            if (commitIndex < auditTrail.LastCommittedEntryIndex)
            {
                try
                {
                    leaderState.ForceReplication();
                }
                catch (NotLeaderException)
                {
                    // local node is not a leader
                    goto exit;
                }
                catch (Exception e)
                {
                    return ValueTask.FromException<long?>(e);
                }
            }

            result = auditTrail.LastCommittedEntryIndex;
        }

    exit:
        return new(result);
    }

    /// <inheritdoc cref="IRaftCluster.ApplyReadBarrierAsync"/>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public async ValueTask ApplyReadBarrierAsync(CancellationToken token = default)
    {
        for (; ; token.ThrowIfCancellationRequested())
        {
            if (state is LeaderState<TMember> leaderState)
            {
                try
                {
                    await leaderState.ForceReplicationAsync(token).ConfigureAwait(false);
                }
                catch (NotLeaderException)
                {
                    // local node is not a leader, retry
                    continue;
                }
            }
            else if (Leader is { } leader)
            {
                if (await leader.SynchronizeAsync(auditTrail.LastCommittedEntryIndex, token).ConfigureAwait(false) is not { } commitIndex)
                    continue;

                await auditTrail.WaitForApplyAsync(commitIndex, token).ConfigureAwait(false);
            }
            else
            {
                throw new QuorumUnreachableException();
            }

            break;
        }
    }

    /// <inheritdoc/>
    async ValueTask<bool> ICluster.ResignAsync(CancellationToken token)
    {
        return await ResignAsync(token).ConfigureAwait(false) ||
            (Leader is { } leader && await leader.ResignAsync(token).ConfigureAwait(false));
    }

    private ValueTask MoveToStandbyState(bool resumable = true)
    {
        Leader = null;
        return UpdateStateAsync(new StandbyState<TMember>(this, LeaderLeaseDuration) { Resumable = resumable });
    }

    async void IRaftStateMachine<TMember>.IncomingHeartbeatTimedOut(IRaftStateMachine.IWeakCallerStateIdentity callerState)
    {
        var lockTaken = false;
        try
        {
            await transitionLock.AcquireAsync(LifecycleToken).ConfigureAwait(false);
            lockTaken = true;

            if (state is StandbyState<TMember> standby && callerState.IsValid(standby))
            {
                if (standby.IsRefreshRequested)
                {
                    standby.Refresh();
                }
                else
                {
                    Leader = null;
                }
            }
        }
        catch (OperationCanceledException) when (lockTaken is false)
        {
            // ignore cancellation of lock acquisition
        }
        catch (ObjectDisposedException) when (lockTaken is false)
        {
            // ignore destroyed lock
        }
        finally
        {
            callerState.Clear();

            if (lockTaken)
                transitionLock.Release();
        }
    }

    /// <inheritdoc />
    async void IRaftStateMachine<TMember>.MoveToFollowerState(IRaftStateMachine.IWeakCallerStateIdentity callerState, bool randomizeTimeout, long? newTerm)
    {
        var lockTaken = false;
        try
        {
            await transitionLock.AcquireAsync(LifecycleToken).ConfigureAwait(false);
            lockTaken = true;

            if (callerState.IsValid(state))
            {
                if (randomizeTimeout)
                    electionTimeout = electionTimeoutProvider.RandomTimeout(random);

                await (newTerm.HasValue
                    ? StepDownAsync(newTerm.GetValueOrDefault(), consensusReached: false)
                    : StepDownAsync(consensusReached: false)).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (lockTaken is false)
        {
            // ignore cancellation of lock acquisition
        }
        catch (ObjectDisposedException) when (lockTaken is false)
        {
            // ignore destroyed lock
        }
        catch (Exception e)
        {
            Logger.TransitionToFollowerStateFailed(e);
            await MoveToStandbyState().ConfigureAwait(false);
        }
        finally
        {
            callerState.Clear();

            if (lockTaken)
                transitionLock.Release();
        }
    }

    /// <inheritdoc />
    async void IRaftStateMachine<TMember>.MoveToCandidateState(IRaftStateMachine.IWeakCallerStateIdentity callerState)
    {
        const byte lockNotTaken = 1;
        const byte lockTaken = 2;

        var lockState = default(byte);
        try
        {
            var currentTerm = Term;

            // Perf: avoid expensive pre-vote phase if refresh requested due to concurrency between inbound Vote
            // and transition to Candidate
            var readyForTransition = await IsReadyForTransitionAsync(currentTerm).ConfigureAwait(false);

            lockState = lockNotTaken;
            await transitionLock.AcquireAsync(LifecycleToken).ConfigureAwait(false);
            lockState = lockTaken;

            if (state is FollowerState<TMember> { IsExpired: true } followerState && callerState.IsValid(followerState))
            {
                Logger.TransitionToCandidateStateStarted(Term, members.Count);

                if (currentTerm == auditTrail.Term && !followerState.IsRefreshRequested)
                {
                    Leader = null;
                }
                else
                {
                    // if term changed after lock then assumes that leader will be updated soon, or
                    // handle concurrency with Vote when the current state is Follower and timeout is about to be refreshed
                    readyForTransition = false;
                }

                if (readyForTransition && TryGetLocalMember()?.Id is { } localMemberId)
                {
                    var newState = new CandidateState<TMember>(this, await auditTrail.IncrementTermAsync(localMemberId, LifecycleToken).ConfigureAwait(false));
                    await UpdateStateAsync(newState).ConfigureAwait(false);

                    // vote for self
                    newState.StartVoting(ElectionTimeout, auditTrail);
                    Logger.TransitionToCandidateStateCompleted(Term);
                }
                else
                {
                    // resume follower state
                    followerState.StartServing(ElectionTimeout);
                    Logger.DowngradedToFollowerState(Term);
                }
            }
        }
        catch (OperationCanceledException) when (lockState is lockNotTaken)
        {
            // ignore cancellation of lock acquisition
        }
        catch (ObjectDisposedException) when (lockState is lockNotTaken)
        {
            // ignore destroyed lock
        }
        catch (Exception e)
        {
            Logger.TransitionToCandidateStateFailed(e);
            await MoveToStandbyState().ConfigureAwait(false);
        }
        finally
        {
            callerState.Clear();

            if (lockState is lockTaken)
                transitionLock.Release();
        }

        Task<bool> IsReadyForTransitionAsync(long currentTerm)
            => state is FollowerState<TMember> { IsExpired: true, IsRefreshRequested: false } followerState && callerState.IsValid(followerState)
                ? PreVoteAsync(currentTerm)
                : Task.FromResult(false);
    }

    /// <inheritdoc />
    async void IRaftStateMachine<TMember>.MoveToLeaderState(IRaftStateMachine.IWeakCallerStateIdentity callerState, TMember newLeader)
    {
        var lockTaken = false;

        try
        {
            Logger.TransitionToLeaderStateStarted(Term);
            await transitionLock.AcquireAsync(LifecycleToken).ConfigureAwait(false);
            lockTaken = true;

            long currentTerm;
            if (state is CandidateState<TMember> candidateState && callerState.IsValid(candidateState) && candidateState.Term == (currentTerm = Term))
            {
                var newState = new LeaderState<TMember>(this, currentTerm, LeaderLeaseDuration)
                {
                    FailureDetectorFactory = FailureDetectorFactory,
                };

                await UpdateStateAsync(newState).ConfigureAwait(false);
                await auditTrail.AppendNoOpEntry(LifecycleToken).ConfigureAwait(false);

                // ensure that the leader is visible to the consumers after no-op entry is added to the log (which acts as a write barrier)
                Leader = newLeader;
                leadershipEvent.TrySetResult(newState.Token);

                newState.StartLeading(HeartbeatTimeout, auditTrail, ConfigurationStorage);
                Logger.TransitionToLeaderStateCompleted(currentTerm);
            }
        }
        catch (OperationCanceledException) when (lockTaken is false)
        {
            // ignore cancellation of lock acquisition
        }
        catch (ObjectDisposedException) when (lockTaken is false)
        {
            // ignore destroyed lock
        }
        catch (Exception e)
        {
            Logger.TransitionToLeaderStateFailed(e);
            await MoveToStandbyState().ConfigureAwait(false);
        }
        finally
        {
            callerState.Clear();

            if (lockTaken)
                transitionLock.Release();
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
    /// <remarks>
    /// This method waits for responses from all available cluster members, not from the majority of them.
    /// </remarks>
    /// <param name="token">The token that can be used to cancel waiting.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="NotLeaderException">The local cluster member is not a leader.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask ForceReplicationAsync(CancellationToken token = default)
        => (state as LeaderState<TMember>)?.ForceReplicationAsync(token) ?? ValueTask.FromException(new NotLeaderException());

    /// <summary>
    /// Appends a new log entry and ensures that it is replicated and committed.
    /// </summary>
    /// <typeparam name="TEntry">The type of the log entry.</typeparam>
    /// <param name="entry">The log entry to be added.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if the appended log entry has been committed by the majority of nodes; <see langword="false"/> if retry is required.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    /// <exception cref="NotLeaderException">The current node is not a leader.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public async ValueTask<bool> ReplicateAsync<TEntry>(TEntry entry, CancellationToken token)
        where TEntry : IRaftLogEntry
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var leaderState = LeaderStateOrException;
        var tokenSource = CombineTokens([token, leaderState.Token]);
        try
        {
            // 1 - append entry to the log
            var index = await auditTrail.AppendAsync(entry, tokenSource.Token).ConfigureAwait(false);

            // 2 - force replication
            leaderState.ForceReplication();

            // 3 - wait for commit
            await auditTrail.WaitForApplyAsync(index, tokenSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException e) when (e.CausedBy(tokenSource, leaderState.Token))
        {
            throw new NotLeaderException(e);
        }
        catch (OperationCanceledException e) when (e.CancellationToken == tokenSource.Token)
        {
            throw new OperationCanceledException(e.Message, e, tokenSource.CancellationOrigin);
        }
        finally
        {
            await tokenSource.DisposeAsync().ConfigureAwait(false);
        }

        return Term == entry.Term;
    }

    private async ValueTask ReplicateAsync(LeaderState<TMember> state, CancellationToken token)
    {
        EmptyLogEntry entry;
        do
        {
            entry = new() { Term = Term };
            var index = await auditTrail.AppendAsync(entry, token).ConfigureAwait(false);
            state.ForceReplication();
            await auditTrail.WaitForApplyAsync(index, token).ConfigureAwait(false);
        }
        while (Term != entry.Term);
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
    IReadOnlySet<EndPoint> IPeerMesh.Peers => new HashSet<EndPoint>(members.Values.Select(static m => m.EndPoint), EndPointComparer);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (!LifecycleToken.IsCancellationRequested)
                Logger.StopAsyncWasNotCalled();

            Dispose(Interlocked.Exchange(ref members, IMemberList.Empty).Values);
            transitionCancellation.Dispose();
            transitionLock.Dispose();
            state.Dispose();
            TrySetDisposedException(readinessProbe);
            ConfigurationStorage.Dispose();

            memberAddedHandlers = memberRemovedHandlers = default;
            leaderChangedHandlers = default;
            TrySetDisposedException(electionEvent);
            TrySetDisposedException(leadershipEvent);
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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Missing = System.Reflection.Missing;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO.Log;
    using Threading;
    using ReplicationCompletedEventHandler = Replication.ReplicationCompletedEventHandler;
    using Sequence = Collections.Generic.Sequence;
    using Timestamp = Diagnostics.Timestamp;

    /// <summary>
    /// Represents transport-independent implementation of Raft protocol.
    /// </summary>
    /// <typeparam name="TMember">The type implementing communication details with remote nodes.</typeparam>
    public abstract partial class RaftCluster<TMember> : Disposable, IRaftCluster, IRaftStateMachine, IAsyncDisposable
        where TMember : class, IRaftClusterMember, IDisposable
    {
        /// <summary>
        /// Represents cluster member.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        protected readonly ref struct MemberHolder
        {
            private readonly Span<MemberList> members;
            private readonly ClusterMemberId id;

            internal MemberHolder(ref MemberList list, ClusterMemberId id)
            {
                if (Unsafe.IsNullRef(ref list))
                {
                    members = default;
                    this.id = default;
                }
                else
                {
                    members = MemoryMarshal.CreateSpan(ref list, 1);
                    this.id = id;
                }
            }

            /// <summary>
            /// Gets actual cluster member.
            /// </summary>
            /// <exception cref="InvalidOperationException">The member is already removed.</exception>
            public TMember Member
            {
                get
                {
                    ref var list = ref MemoryMarshal.GetReference(members);
                    return !Unsafe.IsNullRef(ref list) && list.TryGetValue(id, out var result) ? result : throw new InvalidOperationException();
                }
            }

            /// <summary>
            /// Removes the current member from the list.
            /// </summary>
            /// <remarks>
            /// Removed member is not disposed so it can be reused.
            /// </remarks>
            /// <returns>The removed member.</returns>
            /// <exception cref="InvalidOperationException">Attempt to remove local node; or node is already removed.</exception>
            public TMember Remove()
            {
                ref var list = ref MemoryMarshal.GetReference(members);
                if (Unsafe.IsNullRef(ref list))
                    throw new InvalidOperationException();

                if (MemberList.TryRemove(ref list, id, out var member) && member.IsRemote)
                    return member;

                throw new InvalidOperationException(ExceptionMessages.CannotRemoveLocalNode);
            }

            /// <summary>
            /// Obtains actual cluster member.
            /// </summary>
            /// <param name="holder">The holder of cluster member.</param>
            public static implicit operator TMember(MemberHolder holder) => holder.Member;
        }

        /// <summary>
        /// Represents collection of cluster members stored in the memory of the current process.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        protected readonly ref struct MemberCollectionBuilder
        {
            /// <summary>
            /// Represents enumerator over cluster members.
            /// </summary>
            [StructLayout(LayoutKind.Auto)]
            public readonly ref struct Enumerator
            {
                private readonly Span<MemberList> members;
                private readonly IEnumerator<ClusterMemberId> enumerator;

                internal Enumerator(ref MemberList list)
                {
                    if (Unsafe.IsNullRef(ref list))
                    {
                        members = default;
                        enumerator = Sequence.GetEmptyEnumerator<ClusterMemberId>();
                    }
                    else
                    {
                        members = MemoryMarshal.CreateSpan(ref list, 1);
                        enumerator = list.Keys.GetEnumerator();
                    }
                }

                /// <summary>
                /// Adjusts position of this enumerator.
                /// </summary>
                /// <returns><see langword="true"/> if enumerator moved to the next member successfully; otherwise, <see langword="false"/>.</returns>
                public bool MoveNext() => enumerator.MoveNext();

                /// <summary>
                /// Gets holder of the member holder at the current position of enumerator.
                /// </summary>
                public readonly MemberHolder Current => new(ref MemoryMarshal.GetReference(members), enumerator.Current);

                /// <summary>
                /// Releases all resources associated with this enumerator.
                /// </summary>
                public void Dispose() => enumerator.Dispose();
            }

            private readonly Span<MemberList> members;

            internal MemberCollectionBuilder(ref MemberList list)
            {
                members = MemoryMarshal.CreateSpan(ref list, 1);
            }

            /// <summary>
            /// Adds new cluster member.
            /// </summary>
            /// <param name="member">A new member to be added into in-memory collection.</param>
            public void Add(TMember member)
            {
                ref var list = ref MemoryMarshal.GetReference(members);
                if (!Unsafe.IsNullRef(ref list))
                    list = list.Add(member);
            }

            /// <summary>
            /// Returns enumerator over cluster members.
            /// </summary>
            /// <returns>The enumerator over cluster members.</returns>
            public Enumerator GetEnumerator() => new(ref MemoryMarshal.GetReference(members));
        }

        /// <summary>
        /// Represents mutator of a collection of cluster members.
        /// </summary>
        /// <param name="members">The collection of members maintained by instance of <see cref="RaftCluster{TMember}"/>.</param>
        [Obsolete("Use generic version of this delegate")]
        protected delegate void MemberCollectionMutator(in MemberCollectionBuilder members);

        /// <summary>
        /// Represents mutator of a collection of cluster members.
        /// </summary>
        /// <param name="members">The collection of members maintained by instance of <see cref="RaftCluster{TMember}"/>.</param>
        /// <param name="arg">The argument to be passed to the mutator.</param>
        /// <typeparam name="T">The type of the argument.</typeparam>
        protected delegate void MemberCollectionMutator<T>(in MemberCollectionBuilder members, T arg);

        private readonly bool allowPartitioning;
        private readonly ElectionTimeout electionTimeoutProvider;
        private readonly CancellationTokenSource transitionCancellation;
        private readonly double heartbeatThreshold, clockDriftBound;
        private readonly Random random;
        private bool standbyNode;

        private AsyncLock transitionSync;  // used to synchronize state transitions

        [SuppressMessage("Usage", "CA2213", Justification = "Disposed correctly but cannot be recognized by .NET Analyzer")]
        private volatile RaftState? state;
        private volatile TMember? leader;
        private volatile int electionTimeout;
        private IPersistentState auditTrail;
        private Timestamp lastUpdated; // volatile

        /// <summary>
        /// Initializes a new cluster manager for the local node.
        /// </summary>
        /// <param name="config">The configuration of the local node.</param>
        /// <param name="members">The collection of members that can be modified at construction stage.</param>
        protected RaftCluster(IClusterMemberConfiguration config, out MemberCollectionBuilder members)
        {
            electionTimeoutProvider = config.ElectionTimeout;
            random = new();
            electionTimeout = electionTimeoutProvider.RandomTimeout(random);
            allowPartitioning = config.Partitioning;
            this.members = MemberList.Empty;
            members = new MemberCollectionBuilder(ref this.members);
            transitionSync = AsyncLock.Exclusive();
            transitionCancellation = new CancellationTokenSource();
            LifecycleToken = transitionCancellation.Token;
            auditTrail = new ConsensusOnlyState();
            heartbeatThreshold = config.HeartbeatThreshold;
            standbyNode = config.Standby;
            clockDriftBound = config.ClockDriftBound;
        }

        private static bool IsLocalMember(TMember member) => !member.IsRemote;

        /// <summary>
        /// Gets logger used by this object.
        /// </summary>
        [CLSCompliant(false)]
        protected virtual ILogger Logger => NullLogger.Instance;

        /// <summary>
        /// Gets information the current member.
        /// </summary>
        protected virtual TMember? LocalMember => FindMember(IsLocalMember);

        /// <inheritdoc />
        ILogger IRaftStateMachine.Logger => Logger;

        /// <summary>
        /// Gets election timeout used by the local member.
        /// </summary>
        public TimeSpan ElectionTimeout => TimeSpan.FromMilliseconds(electionTimeout);

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
        /// Gets token that can be used for all internal asynchronous operations.
        /// </summary>
        [Obsolete("Use LifecycleToken property instead")]
        protected CancellationToken Token => LifecycleToken;

        /// <summary>
        /// Gets token that can be used for all internal asynchronous operations.
        /// </summary>
        protected CancellationToken LifecycleToken { get; } // cached to avoid ObjectDisposedException that may be caused by CTS.Token

        /// <summary>
        /// Modifies collection of cluster members.
        /// </summary>
        /// <typeparam name="T">The type of the argument to be passed to the mutator.</typeparam>
        /// <param name="mutator">The action that can be used to change set of cluster members.</param>
        /// <param name="arg">The argument to be passed to the mutator.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        protected async Task ChangeMembersAsync<T>(MemberCollectionMutator<T> mutator, T arg, CancellationToken token)
        {
            using var tokenSource = token.LinkTo(LifecycleToken);
            using var transitionLock = await transitionSync.TryAcquireAsync(token).SuppressDisposedStateOrCancellation().ConfigureAwait(false);
            if (transitionLock)
                ChangeMembers();

            void ChangeMembers()
            {
                var copy = members;
                mutator(new MemberCollectionBuilder(ref copy), arg);
                members = copy;
            }
        }

        /// <summary>
        /// Modifies collection of cluster members.
        /// </summary>
        /// <param name="mutator">The action that can be used to change set of cluster members.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        [Obsolete("Use generic version of this method")]
        protected Task ChangeMembersAsync(MemberCollectionMutator mutator, CancellationToken token)
            => ChangeMembersAsync((in MemberCollectionBuilder builder, Missing arg) => mutator(in builder), Missing.Value, token);

        /// <summary>
        /// Modifies collection of cluster members.
        /// </summary>
        /// <param name="mutator">The action that can be used to change set of cluster members.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        [Obsolete("Use generic version of this method")]
        protected Task ChangeMembersAsync(MemberCollectionMutator mutator)
            => ChangeMembersAsync(mutator, CancellationToken.None);

        /// <summary>
        /// Gets members of Raft-based cluster.
        /// </summary>
        /// <returns>A collection of cluster member.</returns>
        public IReadOnlyCollection<TMember> Members => state is null ? Array.Empty<TMember>() : members;

        /// <inheritdoc />
        IReadOnlyCollection<IRaftClusterMember> IRaftStateMachine.Members => Members;

        /// <inheritdoc/>
        IReadOnlyCollection<IClusterMember> ICluster.Members => Members;

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
        public event ClusterLeaderChangedEventHandler? LeaderChanged;

        /// <summary>
        /// Represents an event raised when the local node completes its replication with another
        /// node.
        /// </summary>
        public event ReplicationCompletedEventHandler? ReplicationCompleted;

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
                if (!ReferenceEquals(oldLeader, value))
                    LeaderChanged?.Invoke(this, value);
            }
        }

        private RaftState CreateInitialState()
            => state = new FollowerState(this) { Metrics = Metrics }.StartServing(ElectionTimeout, LifecycleToken);

        /// <summary>
        /// Starts serving local member.
        /// </summary>
        /// <param name="token">The token that can be used to cancel initialization process.</param>
        /// <returns>The task representing asynchronous execution of the methodC.</returns>
        public virtual async Task StartAsync(CancellationToken token)
        {
            await auditTrail.InitializeAsync(token).ConfigureAwait(false);

            // start active node in Follower state;
            // otherwise use ephemeral state
            state = standbyNode ?
                new StandbyState(this) :
                CreateInitialState();
        }

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

        private async Task StepDown(long newTerm)
        {
            if (newTerm > auditTrail.Term)
                await auditTrail.UpdateTermAsync(newTerm, true).ConfigureAwait(false);
            await StepDown().ConfigureAwait(false);
        }

        private async Task StepDown()
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
        protected async Task<Result<bool>> InstallSnapshotAsync<TSnapshot>(TMember sender, long senderTerm, TSnapshot snapshot, long snapshotIndex, CancellationToken token)
            where TSnapshot : notnull, IRaftLogEntry
        {
            using var tokenSource = token.LinkTo(LifecycleToken);
            using var transitionLock = await transitionSync.AcquireAsync(token).ConfigureAwait(false);
            var currentTerm = auditTrail.Term;
            if (snapshot.IsSnapshot && senderTerm >= currentTerm && snapshotIndex > auditTrail.GetLastIndex(true))
            {
                await StepDown(senderTerm).ConfigureAwait(false);
                Leader = sender;
                await auditTrail.AppendAsync(snapshot, snapshotIndex, token).ConfigureAwait(false);
                return new Result<bool>(currentTerm, true);
            }

            return new Result<bool>(currentTerm, false);
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
        [Obsolete("Use InstallSnapshotAsync method instead")]
        protected Task<Result<bool>> ReceiveSnapshotAsync<TSnapshot>(TMember sender, long senderTerm, TSnapshot snapshot, long snapshotIndex, CancellationToken token)
            where TSnapshot : notnull, IRaftLogEntry
            => InstallSnapshotAsync(sender, senderTerm, snapshot, snapshotIndex, token);

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
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if log entry is committed successfully; <see langword="false"/> if preceding is not present in local audit trail.</returns>
        protected async Task<Result<bool>> AppendEntriesAsync<TEntry>(TMember? sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry
        {
            using var tokenSource = token.LinkTo(LifecycleToken);
            using var transitionLock = await transitionSync.AcquireAsync(token).ConfigureAwait(false);
            var result = false;
            var currentTerm = auditTrail.Term;
            if (currentTerm <= senderTerm)
            {
                Timestamp.VolatileWrite(ref lastUpdated, Timestamp.Current);
                await StepDown(senderTerm).ConfigureAwait(false);
                Leader = sender;
                if (await auditTrail.ContainsAsync(prevLogIndex, prevLogTerm, token).ConfigureAwait(false))
                {
                    var emptySet = entries.RemainingCount > 0L;

                    /*
                    * AppendAsync is called with skipCommitted=true because HTTP response from the previous
                    * replication might fail but the log entry was committed by the local node.
                    * In this case the leader repeat its replication from the same prevLogIndex which is already committed locally.
                    * skipCommitted=true allows to skip the passed committed entry and append uncommitted entries.
                    * If it is 'false' then the method will throw the exception and the node becomes unavailable in each replication cycle.
                    */
                    await auditTrail.AppendAsync(entries, prevLogIndex + 1L, true, token).ConfigureAwait(false);

                    if (commitIndex <= auditTrail.GetLastIndex(true))
                    {
                        // This node is in sync with the leader and no entries arrived
                        if (emptySet && sender is not null)
                            ReplicationCompleted?.Invoke(this, sender);

                        result = true;
                    }
                    else
                    {
                        result = await auditTrail.CommitAsync(commitIndex, token).ConfigureAwait(false) > 0L;
                    }
                }
            }

            return new Result<bool>(currentTerm, result);
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
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if log entry is committed successfully; <see langword="false"/> if preceding is not present in local audit trail.</returns>
        [Obsolete("Use AppendEntriesAsync method instead")]
        protected Task<Result<bool>> ReceiveEntriesAsync<TEntry>(TMember sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry
            => AppendEntriesAsync(sender, senderTerm, entries, prevLogIndex, prevLogTerm, commitIndex, token);

        /// <summary>
        /// Receives preliminary vote from the potential Candidate in the cluster.
        /// </summary>
        /// <param name="nextTerm">Caller's current term + 1.</param>
        /// <param name="lastLogIndex">Index of candidate's last log entry.</param>
        /// <param name="lastLogTerm">Term of candidate's last log entry.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>Pre-vote result received from the member; <see langword="true"/> if the member confirms transition of the caller to Candidate state.</returns>
        protected async Task<Result<bool>> PreVoteAsync(long nextTerm, long lastLogIndex, long lastLogTerm, CancellationToken token)
        {
            bool result;
            long currentTerm;

            // PreVote doesn't cause transition to another Raft state so locking not needed
            var tokenSource = token.LinkTo(LifecycleToken);
            try
            {
                currentTerm = auditTrail.Term;

                // provide leader stickiness
                result = Timestamp.Current - Timestamp.VolatileRead(ref lastUpdated).Value >= ElectionTimeout &&
                    currentTerm <= nextTerm &&
                    await auditTrail.IsUpToDateAsync(lastLogIndex, lastLogTerm, token).ConfigureAwait(false);
            }
            finally
            {
                tokenSource?.Dispose();
            }

            return new Result<bool>(currentTerm, result);
        }

        /// <summary>
        /// Receives preliminary vote from the potential Candidate in the cluster.
        /// </summary>
        /// <param name="nextTerm">Caller's current term + 1.</param>
        /// <param name="lastLogIndex">Index of candidate's last log entry.</param>
        /// <param name="lastLogTerm">Term of candidate's last log entry.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>Pre-vote result received from the member; <see langword="true"/> if the member confirms transition of the caller to Candidate state.</returns>
        [Obsolete("Use PreVoteAsync method instead")]
        protected Task<Result<bool>> ReceivePreVoteAsync(long nextTerm, long lastLogIndex, long lastLogTerm, CancellationToken token)
            => PreVoteAsync(nextTerm, lastLogIndex, lastLogTerm, token);

        /// <summary>
        /// Votes for the new candidate.
        /// </summary>
        /// <param name="sender">The vote sender.</param>
        /// <param name="senderTerm">Term value provided by sender of the request.</param>
        /// <param name="lastLogIndex">Index of candidate's last log entry.</param>
        /// <param name="lastLogTerm">Term of candidate's last log entry.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if local node accepts new leader in the cluster; otherwise, <see langword="false"/>.</returns>
        protected async Task<Result<bool>> VoteAsync(TMember sender, long senderTerm, long lastLogIndex, long lastLogTerm, CancellationToken token)
        {
            var currentTerm = auditTrail.Term;

            if (currentTerm > senderTerm)
                return new(currentTerm, false);

            using var tokenSource = token.LinkTo(LifecycleToken);
            using var transitionLock = await transitionSync.AcquireAsync(token).ConfigureAwait(false);
            var result = false;
            if (currentTerm != senderTerm)
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

        exit:
            return new(currentTerm, result);
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
        [Obsolete("Use VoteAsync method instead")]
        protected Task<Result<bool>> ReceiveVoteAsync(TMember sender, long senderTerm, long lastLogIndex, long lastLogTerm, CancellationToken token)
            => VoteAsync(sender, senderTerm, lastLogIndex, lastLogTerm, token);

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
        /// Revokes leadership of the local node.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/>, if leadership is revoked successfully; otherwise, <see langword="false"/>.</returns>
        [Obsolete("Use ResignAsync method instead")]
        protected Task<bool> ReceiveResignAsync(CancellationToken token)
            => ResignAsync(token);

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

        /// <inheritdoc />
        async void IRaftStateMachine.MoveToFollowerState(bool randomizeTimeout, long? newTerm)
        {
            Debug.Assert(state is not StandbyState);
            using var lockHolder = await transitionSync.TryAcquireAsync(LifecycleToken).SuppressDisposedStateOrCancellation().ConfigureAwait(false);
            if (lockHolder)
            {
                if (randomizeTimeout)
                    electionTimeout = electionTimeoutProvider.RandomTimeout(random);
                await (newTerm.HasValue ? StepDown(newTerm.GetValueOrDefault()) : StepDown()).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        async void IRaftStateMachine.MoveToCandidateState()
        {
            Debug.Assert(state is not StandbyState);

            var currentTerm = auditTrail.Term;
            var readyForTransition = await PreVoteAsync(currentTerm).ConfigureAwait(false);
            using var lockHolder = await transitionSync.TryAcquireAsync(LifecycleToken).SuppressDisposedStateOrCancellation().ConfigureAwait(false);
            if (lockHolder && state is FollowerState followerState && followerState.IsExpired)
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
                    await auditTrail.UpdateVotedForAsync(LocalMember).ConfigureAwait(false);     // vote for self
                    state = new CandidateState(this, await auditTrail.IncrementTermAsync().ConfigureAwait(false)).StartVoting(electionTimeout, auditTrail);
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

            // pre-vote logic that allow to decide about transition to candidate state
            async Task<bool> PreVoteAsync(long currentTerm)
            {
                var lastIndex = auditTrail.GetLastIndex(false);
                var lastTerm = await auditTrail.GetTermAsync(lastIndex, LifecycleToken).ConfigureAwait(false);

                ICollection<Task<Result<bool>>> responses = new LinkedList<Task<Result<bool>>>();
                foreach (var member in Members)
                    responses.Add(member.PreVoteAsync(currentTerm, lastIndex, lastTerm, LifecycleToken));

                var votes = 0;

                // analyze responses
                foreach (var response in responses)
                {
                    try
                    {
                        var result = await response.ConfigureAwait(false);
                        votes += result.Value ? +1 : -1;
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
            }
        }

        /// <inheritdoc />
        async void IRaftStateMachine.MoveToLeaderState(IRaftClusterMember newLeader)
        {
            Debug.Assert(state is not StandbyState);
            Logger.TransitionToLeaderStateStarted();
            using var lockHolder = await transitionSync.TryAcquireAsync(LifecycleToken).SuppressDisposedStateOrCancellation().ConfigureAwait(false);
            long currentTerm;
            if (lockHolder && state is CandidateState candidateState && candidateState.Term == (currentTerm = auditTrail.Term))
            {
                candidateState.Dispose();
                Leader = newLeader as TMember;
                state = new LeaderState(this, allowPartitioning, currentTerm, LeaderLeaseDuration) { Metrics = Metrics }
                    .StartLeading(HeartbeatTimeout, auditTrail, LifecycleToken);
                await auditTrail.AppendNoOpEntry(LifecycleToken).ConfigureAwait(false);
                Metrics?.MovedToLeaderState();
                Logger.TransitionToLeaderStateCompleted();
            }
        }

        /// <summary>
        /// Forces replication.
        /// </summary>
        /// <param name="timeout">The time to wait until replication ends.</param>
        /// <param name="token">The token that can be used to cancel waiting.</param>
        /// <returns><see langword="true"/> if replication is completed; <see langword="false"/>.</returns>
        /// <exception cref="InvalidOperationException">The local cluster member is not a leader.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task<bool> ForceReplicationAsync(TimeSpan timeout, CancellationToken token = default)
            => state is LeaderState leaderState ? leaderState.ForceReplicationAsync(timeout, token) : Task.FromException<bool>(new InvalidOperationException(ExceptionMessages.LocalNodeNotLeader));

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

        private void Cleanup()
        {
            Dispose(Interlocked.Exchange(ref members, MemberList.Empty));
            transitionCancellation.Dispose();
            transitionSync.Dispose();
            leader = null;
            Interlocked.Exchange(ref state, null)?.Dispose();
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
        public ValueTask DisposeAsync() => DisposeAsync(false);
    }
}

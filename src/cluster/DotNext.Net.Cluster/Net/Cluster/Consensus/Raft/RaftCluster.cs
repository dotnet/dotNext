using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO.Log;
    using Threading;
    using static Threading.Tasks.ValueTaskSynchronization;
    using Timestamp = Diagnostics.Timestamp;

    /// <summary>
    /// Represents transport-independent implementation of Raft protocol.
    /// </summary>
    /// <typeparam name="TMember">The type implementing communication details with remote nodes.</typeparam>
    public abstract partial class RaftCluster<TMember> : Disposable, IRaftCluster, IRaftStateMachine, IAsyncDisposable
        where TMember : class, IRaftClusterMember, IDisposable
    {
        private static readonly IMemberCollection EmptyCollection = new EmptyMemberCollection();

        internal interface IMemberCollection : ICollection<TMember>, IReadOnlyCollection<TMember>
        {
        }

        private sealed class EmptyMemberCollection : ReadOnlyCollection<TMember>, IMemberCollection
        {
            internal EmptyMemberCollection()
                : base(Array.Empty<TMember>())
            {
            }
        }

        private sealed class MemberCollection : LinkedList<TMember>, IMemberCollection
        {
            internal MemberCollection()
            {
            }

            internal MemberCollection(IEnumerable<TMember> members)
                : base(members)
            {
            }
        }

        /// <summary>
        /// Represents cluster member.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        protected readonly ref struct MemberHolder
        {
            private readonly LinkedListNode<TMember>? node;

            internal MemberHolder(LinkedListNode<TMember>? node)
                => this.node = node;

            /// <summary>
            /// Gets actual cluster member.
            /// </summary>
            /// <exception cref="InvalidOperationException">The member is already removed.</exception>
            public TMember Member => node?.Value ?? throw new InvalidOperationException();

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
                if (node is null || node.Value is null || node.List is null)
                    throw new InvalidOperationException();

                if (!node.Value.IsRemote)
                    throw new InvalidOperationException(ExceptionMessages.CannotRemoveLocalNode);

                node.List.Remove(node);
                var member = node.Value;
                node.Value = null!;
                return member;
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
            public ref struct Enumerator
            {
                private LinkedListNode<TMember>? current, backlog;

                internal Enumerator(LinkedList<TMember> members)
                {
                    current = null;
                    backlog = members.First;
                }

                /// <summary>
                /// Adjusts position of this enumerator.
                /// </summary>
                /// <returns><see langword="true"/> if enumerator moved to the next member successfully; otherwise, <see langword="false"/>.</returns>
                public bool MoveNext()
                {
                    current = backlog;
                    backlog = backlog?.Next;
                    return current is not null;
                }

                /// <summary>
                /// Gets holder of the member holder at the current position of enumerator.
                /// </summary>
                public readonly MemberHolder Current => new MemberHolder(current);
            }

            private readonly MemberCollection members;

            internal MemberCollectionBuilder(IEnumerable<TMember> members)
                => this.members = new MemberCollection(members);

            internal MemberCollectionBuilder(out IMemberCollection members)
                => members = this.members = new MemberCollection();

            /// <summary>
            /// Adds new cluster member.
            /// </summary>
            /// <param name="member">A new member to be added into in-memory collection.</param>
            public void Add(TMember member) => members.AddLast(member);

            /// <summary>
            /// Returns enumerator over cluster members.
            /// </summary>
            /// <returns>The enumerator over cluster members.</returns>
            public Enumerator GetEnumerator() => new Enumerator(members);

            internal IMemberCollection Build() => members;
        }

        /// <summary>
        /// Represents mutator of collection of members.
        /// </summary>
        /// <param name="members">The collection of members maintained by instance of <see cref="RaftCluster{TMember}"/>.</param>
        protected delegate void MemberCollectionMutator(in MemberCollectionBuilder members);

        private readonly bool allowPartitioning;
        private readonly bool standbyNode;
        private readonly ElectionTimeout electionTimeoutProvider;
        private readonly CancellationTokenSource transitionCancellation;
        private readonly double heartbeatThreshold;
        private volatile IMemberCollection members;

        private AsyncLock transitionSync;  // used to synchronize state transitions

        [SuppressMessage("Usage", "CA2213", Justification = "Disposed correctly but cannot be recognized by .NET Analyzer")]
        private volatile RaftState? state;
        private volatile TMember? leader;
        private volatile int electionTimeout;
        private IPersistentState auditTrail;
        private Timestamp lastUpdated; // access is protected by transitionSync

        /// <summary>
        /// Initializes a new cluster manager for the local node.
        /// </summary>
        /// <param name="config">The configuration of the local node.</param>
        /// <param name="members">The collection of members that can be modified at construction stage.</param>
        protected RaftCluster(IClusterMemberConfiguration config, out MemberCollectionBuilder members)
        {
            electionTimeoutProvider = config.ElectionTimeout;
            electionTimeout = electionTimeoutProvider.RandomTimeout();
            allowPartitioning = config.Partitioning;
            members = new MemberCollectionBuilder(out var collection);
            this.members = collection;
            transitionSync = AsyncLock.Exclusive();
            transitionCancellation = new CancellationTokenSource();
            auditTrail = new ConsensusOnlyState();
            heartbeatThreshold = config.HeartbeatThreshold;
            standbyNode = config.Standby;
        }

        private static bool IsLocalMember(TMember member) => !member.IsRemote;

        /// <summary>
        /// Gets logger used by this object.
        /// </summary>
        [CLSCompliant(false)]
        protected virtual ILogger Logger => NullLogger.Instance;

        /// <inheritdoc />
        ILogger IRaftStateMachine.Logger => Logger;

        /// <summary>
        /// Gets election timeout used by the local member.
        /// </summary>
        public TimeSpan ElectionTimeout => TimeSpan.FromMilliseconds(electionTimeout);

        /// <summary>
        /// Indicates that local member is a leader.
        /// </summary>
        protected bool IsLeaderLocal => state is LeaderState;

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
        protected CancellationToken Token => transitionCancellation.Token;

        private void ChangeMembers(MemberCollectionMutator mutator)
        {
            var members = new MemberCollectionBuilder(this.members);
            mutator(in members);
            this.members = members.Build();
        }

        /// <summary>
        /// Modifies collection of cluster members.
        /// </summary>
        /// <param name="mutator">The action that can be used to change set of cluster members.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        protected async Task ChangeMembersAsync(MemberCollectionMutator mutator, CancellationToken token)
        {
            var tokenSource = token.LinkTo(Token);
            var transitionLock = await transitionSync.TryAcquireAsync(token).ConfigureAwait(false);
            try
            {
                if (transitionLock)
                    ChangeMembers(mutator);
            }
            finally
            {
                transitionLock.Dispose();
                tokenSource?.Dispose();
            }
        }

        /// <summary>
        /// Modifies collection of cluster members.
        /// </summary>
        /// <param name="mutator">The action that can be used to change set of cluster members.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        protected Task ChangeMembersAsync(MemberCollectionMutator mutator)
            => ChangeMembersAsync(mutator, CancellationToken.None);

        /// <summary>
        /// Gets members of Raft-based cluster.
        /// </summary>
        /// <returns>A collection of cluster member.</returns>
        public IReadOnlyCollection<TMember> Members => state is null ? EmptyCollection : members;

        /// <inheritdoc />
        IEnumerable<IRaftClusterMember> IRaftStateMachine.Members => Members;

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

        /// <summary>
        /// Starts serving local member.
        /// </summary>
        /// <param name="token">The token that can be used to cancel initialization process.</param>
        /// <returns>The task representing asynchronous execution of the method.</returns>
        public virtual async Task StartAsync(CancellationToken token)
        {
            await auditTrail.InitializeAsync(token).ConfigureAwait(false);

            // start active node in Follower state;
            // otherwise use ephemeral state
            state = standbyNode ?
                new StandbyState(this) :
                new FollowerState(this) { Metrics = Metrics }.StartServing(ElectionTimeout, Token);
        }

        private async Task CancelPendingRequestsAsync()
        {
            ICollection<Task> tasks = new LinkedList<Task>();
            foreach (var member in members)
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
                Assert(currentState is not null);
                await currentState.StopAsync().ConfigureAwait(false);
                currentState.Dispose();
            }
        }

        private async Task StepDown(long newTerm)
        {
            if (newTerm > auditTrail.Term)
                await WhenAll(auditTrail.UpdateTermAsync(newTerm), auditTrail.UpdateVotedForAsync(null)).ConfigureAwait(false);
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
                    state = newState.StartServing(ElectionTimeout, Token);
                    leaderState.Dispose();
                    Metrics?.MovedToFollowerState();
                    break;
                case CandidateState candidateState:
                    newState = new FollowerState(this) { Metrics = Metrics };
                    await candidateState.StopAsync().ConfigureAwait(false);
                    state = newState.StartServing(ElectionTimeout, Token);
                    candidateState.Dispose();
                    Metrics?.MovedToFollowerState();
                    break;
            }

            Logger.DowngradedToFollowerState();
        }

        /// <summary>
        /// Finds cluster member using predicate.
        /// </summary>
        /// <param name="criteria">The predicate used to find appropriate member.</param>
        /// <returns>The cluster member; or <see langword="null"/> if there is not member matching to the specified criteria.</returns>
        protected TMember? FindMember(Predicate<TMember> criteria)
            => members.FirstOrDefault(criteria.AsFunc());

        /// <summary>
        /// Finds cluster member using predicate.
        /// </summary>
        /// <typeparam name="TArg">The type of the predicate parameter.</typeparam>
        /// <param name="criteria">The predicate used to find appropriate member.</param>
        /// <param name="arg">The argument to be passed to the matching function.</param>
        /// <returns>The cluster member; or <see langword="null"/> if member doesn't exist.</returns>
        protected TMember? FindMember<TArg>(Func<TMember, TArg, bool> criteria, TArg arg)
        {
            foreach (var member in members)
            {
                if (criteria(member, arg))
                    return member;
            }

            return null;
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
        protected async Task<Result<bool>> ReceiveSnapshotAsync<TSnapshot>(TMember sender, long senderTerm, TSnapshot snapshot, long snapshotIndex, CancellationToken token)
            where TSnapshot : IRaftLogEntry
        {
            var tokenSource = token.LinkTo(Token);
            var transitionLock = await transitionSync.AcquireAsync(token).ConfigureAwait(false);
            try
            {
                var currentTerm = auditTrail.Term;
                if (snapshot.IsSnapshot && senderTerm >= currentTerm && snapshotIndex > auditTrail.GetLastIndex(true))
                {
                    await StepDown(senderTerm).ConfigureAwait(false);
                    Leader = sender;
                    await auditTrail.AppendAsync(snapshot, snapshotIndex).ConfigureAwait(false);
                    return new Result<bool>(currentTerm, true);
                }

                return new Result<bool>(currentTerm, false);
            }
            finally
            {
                transitionLock.Dispose();
                tokenSource?.Dispose();
            }
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
        protected async Task<Result<bool>> ReceiveEntriesAsync<TEntry>(TMember sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
            where TEntry : IRaftLogEntry
        {
            var tokenSource = token.LinkTo(Token);
            var transitionLock = await transitionSync.AcquireAsync(token).ConfigureAwait(false);
            try
            {
                var result = false;
                var currentTerm = auditTrail.Term;
                if (currentTerm <= senderTerm)
                {
                    await StepDown(senderTerm).ConfigureAwait(false);
                    Leader = sender;
                    if (await auditTrail.ContainsAsync(prevLogIndex, prevLogTerm, token).ConfigureAwait(false))
                    {
                        /*
                        * AppendAsync is called with skipCommitted=true because HTTP response from the previous
                        * replication might fail but the log entry was committed by the local node.
                        * In this case the leader repeat its replication from the same prevLogIndex which is already committed locally.
                        * skipCommitted=true allows to skip the passed committed entry and append uncommitted entries.
                        * If it is 'false' then the method will throw the exception and the node becomes unavailable in each replication cycle.
                        */
                        await auditTrail.AppendAsync(entries, prevLogIndex + 1L, true, token).ConfigureAwait(false);
                        if (result = commitIndex <= auditTrail.GetLastIndex(true) || await auditTrail.CommitAsync(commitIndex, token).ConfigureAwait(false) > 0)
                            Timestamp.VolatileWrite(ref lastUpdated, Timestamp.Current);
                    }
                }

                return new Result<bool>(currentTerm, result);
            }
            finally
            {
                transitionLock.Dispose();
                tokenSource?.Dispose();
            }
        }

        /// <summary>
        /// Receives preliminary vote from the potential Candidate in the cluster.
        /// </summary>
        /// <param name="nextTerm">Caller's current term + 1.</param>
        /// <param name="lastLogIndex">Index of candidate's last log entry.</param>
        /// <param name="lastLogTerm">Term of candidate's last log entry.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>Pre-vote result received from the member; <see langword="true"/> if the member confirms transition of the caller to Candidate state.</returns>
        protected async Task<Result<bool>> ReceivePreVoteAsync(long nextTerm, long lastLogIndex, long lastLogTerm, CancellationToken token)
        {
            bool result;
            long currentTerm;

            // PreVote doesn't cause transition to another Raft state so locking is not needed
            var tokenSource = token.LinkTo(Token);
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
        /// Votes for the new candidate.
        /// </summary>
        /// <param name="sender">The vote sender.</param>
        /// <param name="senderTerm">Term value provided by sender of the request.</param>
        /// <param name="lastLogIndex">Index of candidate's last log entry.</param>
        /// <param name="lastLogTerm">Term of candidate's last log entry.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if local node accepts new leader in the cluster; otherwise, <see langword="false"/>.</returns>
        protected async Task<Result<bool>> ReceiveVoteAsync(TMember sender, long senderTerm, long lastLogIndex, long lastLogTerm, CancellationToken token)
        {
            var tokenSource = token.LinkTo(Token);
            var transitionLock = await transitionSync.AcquireAsync(token).ConfigureAwait(false);
            try
            {
                var currentTerm = auditTrail.Term;
                if (currentTerm > senderTerm)
                {
                    goto reject;
                }

                if (currentTerm < senderTerm)
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
                    goto reject;
                }

                if (auditTrail.IsVotedFor(sender) && await auditTrail.IsUpToDateAsync(lastLogIndex, lastLogTerm, token).ConfigureAwait(false))
                {
                    await auditTrail.UpdateVotedForAsync(sender).ConfigureAwait(false);
                    return new Result<bool>(currentTerm, true);
                }

                reject:
                return new Result<bool>(currentTerm, false);
            }
            finally
            {
                transitionLock.Dispose();
                tokenSource?.Dispose();
            }
        }

        /// <summary>
        /// Revokes leadership of the local node.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/>, if leadership is revoked successfully; otherwise, <see langword="false"/>.</returns>
        protected async Task<bool> ReceiveResignAsync(CancellationToken token)
        {
            if (standbyNode)
                goto resign_denied;

            var lockHolder = await transitionSync.AcquireAsync(token).ConfigureAwait(false);
            var tokenSource = token.LinkTo(Token);
            try
            {
                if (state is LeaderState leaderState)
                {
                    await leaderState.StopAsync().ConfigureAwait(false);
                    state = new FollowerState(this) { Metrics = Metrics }.StartServing(ElectionTimeout, token);
                    leaderState.Dispose();
                    Leader = null;
                    return true;
                }
            }
            finally
            {
                tokenSource?.Dispose();
                lockHolder.Dispose();
            }

            resign_denied:
            return false;
        }

        /// <inheritdoc/>
        async Task<bool> ICluster.ResignAsync(CancellationToken token)
        {
            if (await ReceiveResignAsync(token).ConfigureAwait(false))
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
            using var lockHolder = await transitionSync.TryAcquireAsync(Token).ConfigureAwait(false);
            if (lockHolder)
            {
                if (randomizeTimeout)
                    electionTimeout = electionTimeoutProvider.RandomTimeout();
                await (newTerm.HasValue ? StepDown(newTerm.GetValueOrDefault()) : StepDown()).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        async void IRaftStateMachine.MoveToCandidateState()
        {
            Debug.Assert(state is not StandbyState);
            Logger.TransitionToCandidateStateStarted();
            using var lockHolder = await transitionSync.TryAcquireAsync(Token).ConfigureAwait(false);
            if (lockHolder && state is FollowerState followerState)
            {
                Leader = null;
                if (await PreVoteAsync().ConfigureAwait(false))
                {
                    followerState.Dispose();
                    var localMember = FindMember(IsLocalMember);
                    await auditTrail.UpdateVotedForAsync(localMember).ConfigureAwait(false);     // vote for self
                    state = new CandidateState(this, await auditTrail.IncrementTermAsync().ConfigureAwait(false)).StartVoting(electionTimeout, auditTrail);
                    Metrics?.MovedToCandidateState();
                }
                else
                {
                    // resume follower state
                    followerState.StartServing(ElectionTimeout, Token);
                }

                Logger.TransitionToCandidateStateCompleted();
            }

            // pre-vote logic that allow to decide about transition to candidate state
            async Task<bool> PreVoteAsync()
            {
                var currentTerm = auditTrail.Term;
                var lastIndex = auditTrail.GetLastIndex(false);
                var lastTerm = await auditTrail.GetTermAsync(lastIndex, Token).ConfigureAwait(false);

                ICollection<Task<Result<bool>>> responses = new LinkedList<Task<Result<bool>>>();
                foreach (var member in Members)
                    responses.Add(member.PreVoteAsync(currentTerm, lastIndex, lastTerm, Token));

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
            using var lockHolder = await transitionSync.TryAcquireAsync(Token).ConfigureAwait(false);
            long currentTerm;
            if (lockHolder && state is CandidateState candidateState && candidateState.Term == (currentTerm = auditTrail.Term))
            {
                candidateState.Dispose();
                Leader = newLeader as TMember;
                state = new LeaderState(this, allowPartitioning, currentTerm) { Metrics = Metrics }
                    .StartLeading(TimeSpan.FromMilliseconds(electionTimeout * heartbeatThreshold), auditTrail, Token);
                await auditTrail.AppendNoOpEntry(Token).ConfigureAwait(false);
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

        private void Cleanup()
        {
            ICollection<TMember> members = Interlocked.Exchange(ref this.members, EmptyCollection);
            Dispose(members);
            if (members.Count > 0)
                members.Clear();
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

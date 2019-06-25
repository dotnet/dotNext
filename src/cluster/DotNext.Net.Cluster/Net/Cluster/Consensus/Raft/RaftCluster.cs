using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Replication;
    using Threading;
    using static Threading.Tasks.Continuation;

    /// <summary>
    /// Represents transport-independent implementation of Raft protocol.
    /// </summary>
    public abstract class RaftCluster<TMember> : Disposable, IRaftCluster, IRaftStateMachine
        where TMember : class, IRaftClusterMember, IDisposable
    {
        private static readonly Action<TMember> CancelPendingRequests = DelegateHelpers.CreateOpenDelegate<Action<TMember>>(member => member.CancelPendingRequests());

        /// <summary>
        /// Represents cluster member.
        /// </summary>
        protected readonly ref struct MemberHolder
        {
            private readonly LinkedListNode<TMember> node;

            internal MemberHolder(LinkedListNode<TMember> node) => this.node = node;

            /// <summary>
            /// Gets actual cluster member.
            /// </summary>
            public TMember Member => node?.Value;

            /// <summary>
            /// Removes the current member from the list.
            /// </summary>
            /// <remarks>
            /// Removed member is not disposed so it can be reused.
            /// </remarks>
            /// <returns>The removed member.</returns>
            /// <exception cref="InvalidOperationException">Attempt to remove local node.</exception>
            public TMember Remove()
            {
                if (node is null)
                    return null;
                else if (!node.Value.IsRemote)
                    throw new InvalidOperationException(ExceptionMessages.CannotRemoveLocalNode);
                else
                {
                    node.List.Remove(node);
                    var member = node.Value;
                    node.Value = null;
                    return member;
                }
            }

            /// <summary>
            /// Obtains actual cluster member.
            /// </summary>
            /// <param name="holder"></param>
            public static implicit operator TMember(MemberHolder holder) => holder.Member;
        }

        /// <summary>
        /// Represents collection of cluster members stored in the memory of the current process.
        /// </summary>
        protected readonly ref struct MutableMemberCollection
        {
            /// <summary>
            /// Represents enumerator over cluster members.
            /// </summary>
            public ref struct Enumerator
            {
                private LinkedListNode<TMember> current;
                private bool started;

                internal Enumerator(LinkedList<TMember> members)
                {
                    current = members.First;
                    started = false;
                }

                /// <summary>
                /// Adjusts position of this enumerator.
                /// </summary>
                /// <returns><see langword="true"/> if enumerator moved to the next member successfully; otherwise, <see langword="false"/>.</returns>
                public bool MoveNext()
                {
                    if (started)
                        current = current.Next;
                    else
                        started = true;
                    return !(current is null);
                }

                /// <summary>
                /// Gets holder of the member holder at the current position of enumerator.
                /// </summary>
                public MemberHolder Current => new MemberHolder(current);
            }

            private readonly LinkedList<TMember> members;

            internal MutableMemberCollection(IEnumerable<TMember> members) => this.members = new LinkedList<TMember>(members);

            internal MutableMemberCollection(out ICollection<TMember> members) 
                => members = this.members = new LinkedList<TMember>();

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

            internal LinkedList<TMember> AsLinkedList() => members;
        }

        /// <summary>
        /// Represents mutator of collection of members.
        /// </summary>
        /// <param name="members">The collection of members maintained by instance of <see cref="RaftCluster{TMember}"/>.</param>
        protected delegate void MemberCollectionMutator(in MutableMemberCollection members);


        private volatile ICollection<TMember> members;

        private AsyncLock transitionSync;  //used to synchronize state transitions
        private volatile RaftState state;
        private volatile TMember votedFor, leader;
        private long currentTerm;
        private readonly bool absoluteMajority;
        private readonly ElectionTimeout electionTimeoutProvider;
        private volatile int electionTimeout;
        private readonly CancellationTokenSource transitionCancellation;
        private IPersistentState auditTrail;

        /// <summary>
        /// Initializes a new cluster manager for the local node.
        /// </summary>
        /// <param name="config">The configuration of the local node.</param>
        /// <param name="members">The collection of members that can be modified at construction stage.</param>
        protected RaftCluster(IClusterMemberConfiguration config, out MutableMemberCollection members)
        {
            electionTimeoutProvider = config.ElectionTimeout;
            electionTimeout = electionTimeoutProvider.RandomTimeout();
            absoluteMajority = config.AbsoluteMajority;
            members = new MutableMemberCollection(out var collection);
            this.members = collection;
            transitionSync = AsyncLock.Exclusive();
            transitionCancellation = new CancellationTokenSource();
        }

        private static bool IsLocalMember(TMember member) => !member.IsRemote;

        /// <summary>
        /// Gets logger used by this object.
        /// </summary>
        [CLSCompliant(false)]
        protected virtual ILogger Logger => NullLogger.Instance;

        ILogger IRaftStateMachine.Logger => Logger;

        TimeSpan IRaftCluster.ElectionTimeout => TimeSpan.FromMilliseconds(electionTimeout);

        /// <summary>
        /// Indicates that local member is a leader.
        /// </summary>
        protected bool IsLeaderLocal => state is LeaderState;

        /// <summary>
        /// Associates audit trail with the current instance.
        /// </summary>
        public IPersistentState AuditTrail
        {
            set => auditTrail = auditTrail is null ? value : throw new InvalidOperationException(ExceptionMessages.AuditTrailAlreadyDefined);
            protected get => auditTrail;
        }

        /// <summary>
        /// Modifies collection of cluster members.
        /// </summary>
        /// <param name="mutator">The action that can be used to change set of cluster members.</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        protected void ChangeMembers(MemberCollectionMutator mutator)
        {
            var members = new MutableMemberCollection(this.members);
            mutator(in members);
            this.members = members.AsLinkedList();
        }

        /// <summary>
        /// Gets members of Raft-based cluster.
        /// </summary>
        /// <returns>A collection of cluster member.</returns>
        public IReadOnlyCollection<TMember> Members => state is null ? Array.Empty<TMember>() : (IReadOnlyCollection<TMember>)members;

        IEnumerable<IRaftClusterMember> IRaftStateMachine.Members => Members;

        IReadOnlyCollection<IClusterMember> ICluster.Members => Members;

        /// <summary>
        /// Gets Term value maintained by local member.
        /// </summary>
        public long Term => currentTerm.VolatileRead();

        /// <summary>
        /// An event raised when leader has been changed.
        /// </summary>
        public event ClusterLeaderChangedEventHandler LeaderChanged;

        IClusterMember ICluster.Leader => Leader;

        /// <summary>
        /// Gets leader of the cluster.
        /// </summary>
        public TMember Leader
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
            if (auditTrail is null)
            {
                votedFor = null;
                currentTerm.VolatileWrite(0L);
            }
            else
            {
                foreach (var member in members)
                    if (await auditTrail.IsVotedForAsync(member).ConfigureAwait(false))
                        votedFor = member;

                currentTerm.VolatileWrite(await auditTrail.RestoreTermAsync().ConfigureAwait(false));
            }
            //start node in Follower state
            state = new FollowerState(this, electionTimeout);
        }

        /// <summary>
        /// Stops serving local member.
        /// </summary>
        /// <param name="token">The token that can be used to cancel shutdown process.</param>
        /// <returns>The task representing asynchronous execution of the method.</returns>
        public virtual async Task StopAsync(CancellationToken token)
        {
            transitionCancellation.Cancel(false);
            members.ForEach(CancelPendingRequests);
            leader = votedFor = null;
            using (await transitionSync.Acquire(token).ConfigureAwait(false))
                switch (Interlocked.Exchange(ref state, null))
                {
                    case FollowerState followerState:
                        followerState.Dispose();
                        return;
                    case CandidateState candidateState:
                        await candidateState.StopVoting().OnCompleted().ConfigureAwait(false);
                        candidateState.Dispose();
                        return;
                    case LeaderState leaderState:
                        await leaderState.StopLeading(token).OnCompleted().ConfigureAwait(false);
                        leaderState.Dispose();
                        return;
                }
        }

        private async Task StepDown()
        {
            Logger.DowngradingToFollowerState();
            switch (state)
            {
                case LeaderState leaderState:
                    state = new FollowerState(this, electionTimeout);
                    await leaderState.StopLeading(transitionCancellation.Token).ConfigureAwait(false);
                    leaderState.Dispose();
                    break;
                case CandidateState candidateState:
                    state = new FollowerState(this, electionTimeout);
                    await candidateState.StopVoting().ConfigureAwait(false);
                    candidateState.Dispose();
                    break;
                case FollowerState followerState:
                    followerState.Refresh();
                    break;
            }
            Logger.DowngradedToFollowerState();
        }

        /// <summary>
        /// Finds cluster member using predicate.
        /// </summary>
        /// <param name="matcher">The predicate used to find appropriate member.</param>
        /// <returns>The cluster member; </returns>
        protected TMember FindMember(Predicate<TMember> matcher)
            => members.FirstOrDefault(matcher.AsFunc());
        
        private async ValueTask<bool> TrySetTerm(long senderTerm, bool eraseOutdatedLeader) //likely to be completed synchronously
        {
            var comparison = currentTerm.VolatileRead().CompareTo(senderTerm);
            if (comparison > 0) //currentTerm > term
                return false;
            if (comparison < 0) //currentTerm < term
            {
                currentTerm.VolatileWrite(senderTerm);
                await SaveTermAsync(auditTrail, senderTerm).ConfigureAwait(false);
                await StepDown().ConfigureAwait(false);
                if(eraseOutdatedLeader)
                    Leader = null;
                if(Interlocked.Exchange(ref votedFor, null) != null)
                    await SaveLastVoteAsync(auditTrail, null);
            }
            return true;
        }

        /// <summary>
        /// Handles Heartbeat message received from remote cluster member.
        /// </summary>
        /// <param name="sender">The sender of Heartbeat message.</param>
        /// <param name="senderTerm">Term value provided by Heartbeat message sender.</param>
        /// <returns>The task representing asynchronous execution of the method.</returns>
        protected async Task ReceiveHeartbeat(TMember sender, long senderTerm)
        {
            using (await transitionSync.Acquire(transitionCancellation.Token).ConfigureAwait(false))
                if (await TrySetTerm(senderTerm, false).ConfigureAwait(false))
                {
                    (state as FollowerState)?.Refresh();
                    Leader = sender;
                }
        }

        /// <summary>
        /// Handles AppendEntries message received from remote cluster member.
        /// </summary>
        /// <param name="sender">The sender of the replica message.</param>
        /// <param name="senderTerm">Term value provided by Heartbeat message sender.</param>
        /// <param name="newEntry">A new entry to be committed locally.</param>
        /// <param name="precedingEntry">The identifier of the log entry immediately preceding new one.</param>
        /// <returns><see langword="true"/> if log entry is committed successfully; <see langword="false"/> <paramref name="precedingEntry"/> is not present in local audit trail.</returns>
        protected async Task<bool> ReceiveEntries(TMember sender, long senderTerm, ILogEntry<LogEntryId> newEntry, LogEntryId precedingEntry)
        {
            using (await transitionSync.Acquire(transitionCancellation.Token).ConfigureAwait(false))
                if(await TrySetTerm(senderTerm, false).ConfigureAwait(false) && auditTrail.Contains(precedingEntry))
                {
                    (state as FollowerState)?.Refresh();
                    Leader = sender;
                    await auditTrail.CommitAsync(newEntry);
                    return true;
                }
                else
                    return false;
        }

        /// <summary>
        /// Send a new log entry to all cluster members if the local member is leader.
        /// </summary>
        /// <param name="entry">A new log entry.</param>
        /// <param name="token">The token that can be used to cancel this operation.</param>
        /// <returns>The task representing asynchronous execution of the method.</returns>
        /// <exception cref="AggregateException">Unable to replicate one or more cluster nodes. You can analyze inner exceptions which are derive from <see cref="ConsensusProtocolException"/> or <see cref="ReplicationException"/>.</exception>
        /// <exception cref="InvalidOperationException">The caller application is not a leader node.</exception>
        /// <exception cref="NotSupportedException">Audit trail is not defined for this instance.</exception>
        public Task ReplicateAsync(ILogEntry<LogEntryId> entry, CancellationToken token)
        {
            if (auditTrail is null)
                throw new NotSupportedException();
            else if (state is LeaderState leaderState)
                return leaderState.AppendEntriesAsync(entry, auditTrail, token);
            else
                throw new InvalidOperationException(ExceptionMessages.IsNotLeader);
        }

        private static bool CheckLogEntry(IAuditTrail<LogEntryId> trail, LogEntryId senderLastEntry)
        {
            var localLastEntry = trail.LastRecord;
            return senderLastEntry.Index >= localLastEntry.Index && senderLastEntry.Term >= localLastEntry.Term;
        }

        private static bool CheckLogEntry(IAuditTrail<LogEntryId> trail, LogEntryId? senderLastEntry)
        {
            if(senderLastEntry is null)
                return trail is null;
            else if(trail is null)
                return false;
            else
                return CheckLogEntry(trail, senderLastEntry.Value);
        }

        private static ValueTask SaveTermAsync(IPersistentState state, long term) => (state?.SaveTermAsync(term)).GetValueOrDefault();

        private static ValueTask SaveLastVoteAsync(IPersistentState state, TMember member) => (state?.SaveVotedForAsync(member)).GetValueOrDefault();

        /// <summary>
        /// Votes for the new candidate.
        /// </summary>
        /// <param name="sender">The vote sender.</param>
        /// <param name="senderTerm">Term value provided by sender of the request.</param>
        /// <param name="senderLastEntry">The last log entry stored on the sender.</param>
        /// <returns><see langword="true"/> if local node accepts new leader in the cluster; otherwise, <see langword="false"/>.</returns>
        protected async Task<bool> ReceiveVote(TMember sender, long senderTerm, LogEntryId? senderLastEntry)
        {
            using (await transitionSync.Acquire(transitionCancellation.Token).ConfigureAwait(false))
                if(await TrySetTerm(senderTerm, true).ConfigureAwait(false) && (votedFor is null || ReferenceEquals(votedFor, sender)) && CheckLogEntry(auditTrail, senderLastEntry))
                {
                    (state as FollowerState)?.Refresh();
                    if(Interlocked.Exchange(ref votedFor, sender) is null)
                        await SaveLastVoteAsync(auditTrail, sender);
                    return true;
                }
                else
                    return false;
        }

        private async Task<bool> ResignAsync(CancellationToken token)
        {
            using (await transitionSync.Acquire(token).ConfigureAwait(false))
                if (state is LeaderState leaderState)
                {
                    await leaderState.StopLeading(token).ConfigureAwait(false);
                    leaderState.Dispose();
                    state = new FollowerState(this, electionTimeout);
                    Leader = null;
                    return true;
                }
                else
                    return false;
        }

        async Task<bool> ICluster.ResignAsync(CancellationToken token)
        {
            using (var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, transitionCancellation.Token))
            {
                if (await ResignAsync(tokenSource.Token).ConfigureAwait(false))
                {
                    var leader = Leader;
                    return !(leader is null) && await leader.ResignAsync(tokenSource.Token).ConfigureAwait(false);
                }

                return false;
            }
        }

        /// <summary>
        /// Revokes leadership of the local node.
        /// </summary>
        /// <returns><see langword="true"/>, if leadership is revoked successfully; otherwise, <see langword="false"/>.</returns>
        protected Task<bool> ReceiveResign() => ResignAsync(transitionCancellation.Token);

        async void IRaftStateMachine.MoveToFollowerState(bool randomizeTimeout)
        {
            Logger.TransitionToFollowerStateStarted();
            using (var lockHolder = await transitionSync.TryAcquire(transitionCancellation.Token).ConfigureAwait(false))
                if (lockHolder)
                {

                    if (randomizeTimeout)
                        electionTimeout = electionTimeoutProvider.RandomTimeout();
                    switch (state)
                    {
                        case LeaderState leaderState:
                            state = new FollowerState(this, electionTimeout);
                            await leaderState.StopLeading(transitionCancellation.Token).ConfigureAwait(false);
                            leaderState.Dispose();
                            break;
                        case CandidateState candidateState:
                            state = new FollowerState(this, electionTimeout);
                            await candidateState.StopVoting().ConfigureAwait(false);
                            candidateState.Dispose();
                            break;
                    }
                }

            Logger.TransitionToFollowerStateCompleted();
        }

        async void IRaftStateMachine.MoveToCandidateState()
        {
            Logger.TransitionToCandidateStateStarted();
            using (var lockHolder = await transitionSync.TryAcquire(transitionCancellation.Token).ConfigureAwait(false))
                if (lockHolder && state is FollowerState followerState)
                {
                    followerState.Dispose();
                    Leader = null;
                    var newState = new CandidateState(this, absoluteMajority, currentTerm.IncrementAndGet());
                    var localMember = FindMember(IsLocalMember);
                    votedFor = localMember;   //vote for self
                    await SaveLastVoteAsync(auditTrail, localMember).ConfigureAwait(false);
                    newState.StartVoting(electionTimeout, auditTrail);
                    state = newState;
                    Logger.TransitionToCandidateStateCompleted();
                }
        }

        async void IRaftStateMachine.MoveToLeaderState(IRaftClusterMember newLeader)
        {
            Logger.TransitionToLeaderStateStarted();
            using (var lockHolder = await transitionSync.Acquire(transitionCancellation.Token).ConfigureAwait(false))
            {
                long term;
                if (lockHolder && state is CandidateState candidateState && candidateState.Term == (term = currentTerm.VolatileRead()))
                {
                    candidateState.Dispose();
                    var newState = new LeaderState(this, absoluteMajority, TimeSpan.FromMilliseconds(electionTimeout / 2D), term);
                    newState.StartLeading();
                    state = newState;
                    Leader = newLeader as TMember;
                    Logger.TransitionToLeaderStateCompleted();
                }
            }
        }

        /// <summary>
        /// Releases managed and unmanaged resources associated with this object.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> if called from <see cref="Disposable.Dispose()"/>; <see langword="false"/> if called from finalizer <see cref="Disposable.Finalize()"/>.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                var members = Interlocked.Exchange(ref this.members, Array.Empty<TMember>());
                Dispose(members);
                if (members.Count > 0)
                    members.Clear();
                transitionCancellation.Dispose();
                transitionSync.Dispose();
                leader = votedFor = null;
                Interlocked.Exchange(ref state, null)?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Net.Cluster.Replication;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Threading;
    using static Threading.Tasks.ValueTaskSynchronization;
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
        private volatile TMember leader;
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
            auditTrail = new InMemoryAuditTrail();
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

        
        IAuditTrail<ILogEntry> IReplicationCluster<ILogEntry>.AuditTrail => auditTrail;

        /// <summary>
        /// Associates audit trail with the current instance.
        /// </summary>
        public IPersistentState AuditTrail
        {
            set
            {
                if(auditTrail is InMemoryAuditTrail && value is null)
                    return;
                auditTrail = value;
            }
            get => auditTrail;
        }

        /// <summary>
        /// Gets token that can be used for all internal asynchronous operations.
        /// </summary>
        protected CancellationToken Token => transitionCancellation.Token;

        private void ChangeMembers(MemberCollectionMutator mutator)
        {
            var members = new MutableMemberCollection(this.members);
            mutator(in members);
            this.members = members.AsLinkedList();
        }

        /// <summary>
        /// Modifies collection of cluster members.
        /// </summary>
        /// <param name="mutator">The action that can be used to change set of cluster members.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        protected async Task ChangeMembersAsync(MemberCollectionMutator mutator)
        {
            using(var holder = await transitionSync.TryAcquire(transitionCancellation.Token).ConfigureAwait(false))
                if(holder)
                    ChangeMembers(mutator);
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
        public long Term => auditTrail.Term;

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
        public virtual Task StartAsync(CancellationToken token)
        {
            //start node in Follower state
            state = new FollowerState(this).StartServing(electionTimeout);
            return Task.CompletedTask;
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
            leader = null;
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
                        await leaderState.StopLeading().OnCompleted().ConfigureAwait(false);
                        leaderState.Dispose();
                        return;
                }
        }

        private async Task StepDown(long newTerm) //true - need to update leader, false - leave leader value as is
        {
            if(newTerm > auditTrail.Term)
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
                    var newState = new FollowerState(this);
                    await leaderState.StopLeading().ConfigureAwait(false);
                    leaderState.Dispose();
                    state = newState.StartServing(electionTimeout);
                    break;
                case CandidateState candidateState:
                    newState = new FollowerState(this);
                    await candidateState.StopVoting().ConfigureAwait(false);
                    candidateState.Dispose();
                    state = newState.StartServing(electionTimeout);
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

        /// <summary>
        /// Handles AppendEntries message received from remote cluster member.
        /// </summary>
        /// <param name="sender">The sender of the replica message.</param>
        /// <param name="senderTerm">Term value provided by Heartbeat message sender.</param>
        /// <param name="entries">The entries to be committed locally.</param>
        /// <param name="prevLogIndex">Index of log entry immediately preceding new ones.</param>
        /// <param name="prevLogTerm">Term of <paramref name="prevLogIndex"/> entry.</param>
        /// <param name="commitIndex">The last entry known to be committed on the sender side.</param>
        /// <returns><see langword="true"/> if log entry is committed successfully; <see langword="false"/> if preceding is not present in local audit trail.</returns>
        protected async Task<Result<bool>> ReceiveEntries(TMember sender, long senderTerm, IReadOnlyList<ILogEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex)
        {
            using (await transitionSync.Acquire(transitionCancellation.Token).ConfigureAwait(false))
                if (auditTrail.Term <= senderTerm &&
                    await auditTrail.ContainsAsync(prevLogIndex, prevLogTerm).ConfigureAwait(false))
                {
                    await StepDown(senderTerm).ConfigureAwait(false);
                    Leader = sender;

                    if (entries.Count > 0L)
                        await auditTrail.AppendAsync(entries, prevLogIndex + 1L).ConfigureAwait(false);

                    var currentIndex = auditTrail.GetLastIndex(true);
                    var result = commitIndex <= currentIndex ||
                                 await auditTrail.CommitAsync(currentIndex + 1L, commitIndex).ConfigureAwait(false) > 0;
                    return new Result<bool>(auditTrail.Term, result);
                }
                else
                    return new Result<bool>(auditTrail.Term, false);
        }

        /// <summary>
        /// Votes for the new candidate.
        /// </summary>
        /// <param name="sender">The vote sender.</param>
        /// <param name="senderTerm">Term value provided by sender of the request.</param>
        /// <param name="lastLogIndex">Index of candidate's last log entry.</param>
        /// <param name="lastLogTerm">Term of candidate's last log entry.</param>
        /// <returns><see langword="true"/> if local node accepts new leader in the cluster; otherwise, <see langword="false"/>.</returns>
        protected async Task<Result<bool>> ReceiveVote(TMember sender, long senderTerm, long lastLogIndex, long lastLogTerm)
        {
            using (await transitionSync.Acquire(transitionCancellation.Token).ConfigureAwait(false))
            {
                if (auditTrail.Term > senderTerm) //currentTerm > term
                    return new Result<bool>(auditTrail.Term, false);
                if(auditTrail.Term < senderTerm)
                {
                    Leader = null;
                    await StepDown(senderTerm).ConfigureAwait(false);
                }
                else
                    (state as FollowerState)?.Refresh();
                if(auditTrail.IsVotedFor(sender) && await auditTrail.IsUpToDateAsync(lastLogIndex, lastLogTerm).ConfigureAwait(false))
                {
                    await auditTrail.UpdateVotedForAsync(sender).ConfigureAwait(false);
                    return new Result<bool>(auditTrail.Term, true);
                }
                return new Result<bool>(auditTrail.Term, false);
            }
        }

        private async Task<bool> ResignAsync(CancellationToken token)
        {
            using (await transitionSync.Acquire(token).ConfigureAwait(false))
                if (state is LeaderState leaderState)
                {
                    await leaderState.StopLeading().ConfigureAwait(false);
                    leaderState.Dispose();
                    state = new FollowerState(this).StartServing(electionTimeout);
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

        async void IRaftStateMachine.MoveToFollowerState(bool randomizeTimeout, long? newTerm)
        {
            Logger.TransitionToFollowerStateStarted();
            using (var lockHolder = await transitionSync.TryAcquire(transitionCancellation.Token).ConfigureAwait(false))
                if (lockHolder)
                {
                    if (randomizeTimeout)
                        electionTimeout = electionTimeoutProvider.RandomTimeout();
                    await (newTerm.HasValue ? StepDown(newTerm.Value) : StepDown()).ConfigureAwait(false);
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
                    var localMember = FindMember(IsLocalMember);
                    await auditTrail.UpdateVotedForAsync(localMember).ConfigureAwait(false);     //vote for self
                    state = new CandidateState(this, absoluteMajority, await auditTrail.IncrementTermAsync().ConfigureAwait(false)).StartVoting(electionTimeout, auditTrail);
                    Logger.TransitionToCandidateStateCompleted();
                }
        }

        [SuppressMessage("Reliability", "CA2000", Justification = "The instance returned by StartLeading is the same as 'this'")]
        async void IRaftStateMachine.MoveToLeaderState(IRaftClusterMember newLeader)
        {
            Logger.TransitionToLeaderStateStarted();
            using (var lockHolder = await transitionSync.Acquire(transitionCancellation.Token).ConfigureAwait(false))
            {
                if (lockHolder && state is CandidateState candidateState && candidateState.Term == auditTrail.Term)
                {
                    candidateState.Dispose();
                    Leader = newLeader as TMember;
                    state = new LeaderState(this, absoluteMajority, auditTrail.Term).StartLeading(electionTimeout / 2,
                        auditTrail);
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
                leader = null;
                Interlocked.Exchange(ref state, null)?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

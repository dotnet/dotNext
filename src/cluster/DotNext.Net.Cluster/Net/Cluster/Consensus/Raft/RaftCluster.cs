using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Net.Cluster.Replication;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Messaging;
    using Threading;

    /// <summary>
    /// Represents transport-independent implementation of Raft protocol.
    /// </summary>
    public abstract class RaftCluster<TMember> : Disposable, IRaftCluster, IRaftStateMachine
        where TMember : class, IRaftClusterMember
    {
        protected delegate bool MemberMatcher<MemberId>(TMember member, MemberId id);

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
                if(node is null)
                    return null;
                else if(!node.Value.IsRemote)
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
        protected readonly ref struct MemberCollection
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

            internal MemberCollection(IEnumerable<TMember> members) => this.members = new LinkedList<TMember>(members);

            internal MemberCollection(LinkedList<TMember> members) => this.members = members;

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
        protected delegate void MemberCollectionMutator(in MemberCollection members);


        private volatile ICollection<TMember> members;
        
        private AsyncLock transitionSync;  //used to synchronize state transitions
        private volatile RaftState state;
        private TMember votedFor;
        private volatile IRaftClusterMember leader;
        private long consensusTerm;
        private readonly bool absoluteMajority;
        private readonly ElectionTimeout electionTimeoutProvider;
        private volatile int electionTimeout;
        private readonly CancellationTokenSource transitionCancellation;
        private IAuditTrail auditTrail;

        /// <summary>
        /// Initializes a new cluster manager for the local node.
        /// </summary>
        /// <param name="config">The configuration of the local node.</param>
        /// <param name="members">The collection of members that can be modified at construction stage.</param>
        protected RaftCluster(IClusterMemberConfiguration config, out MemberCollection members)
        {
            electionTimeoutProvider = config.ElectionTimeout;
            electionTimeout = electionTimeoutProvider.RandomTimeout();
            absoluteMajority = config.AbsoluteMajority;
            members = new MemberCollection(this.members = new LinkedList<TMember>());
            transitionSync = AsyncLock.Exclusive();
            transitionCancellation = new CancellationTokenSource();
        }

        public IAuditTrail AuditTrail
        {
            set => auditTrail = auditTrail is null ? value : throw new InvalidOperationException(ExceptionMessages.AuditTrailAlreadyDefined);
        }

        /// <summary>
        /// Modifies collection of cluster members.
        /// </summary>
        /// <param name="mutator">The action that can be used to change set of cluster members.</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        protected void ChangeMembers(MemberCollectionMutator mutator)
        {
            var members = new MemberCollection(this.members);
            mutator(in members);
            this.members = members.AsLinkedList();
        }

        /// <summary>
        /// Gets typed collection of cluster members.
        /// </summary>
        protected IReadOnlyCollection<TMember> Members => state is null ? Array.Empty<TMember>() : (IReadOnlyCollection<TMember>)members;

        IEnumerable<IRaftClusterMember> IRaftStateMachine.Members => Members;

        int IReadOnlyCollection<IClusterMember>.Count => Members.Count;

        IEnumerator<IClusterMember> IEnumerable<IClusterMember>.GetEnumerator() => Members.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => Members.GetEnumerator();

        protected TMember VotedFor => votedFor;

        /// <summary>
        /// Gets Term value
        /// </summary>
        public long Term => consensusTerm.VolatileRead();
        
        public event ClusterLeaderChangedEventHandler LeaderChanged;
        public abstract event ClusterMemberStatusChanged MemberStatusChanged;

        IClusterMember ICluster.Leader => Leader;

        /// <summary>
        /// Gets leader of the cluster.
        /// </summary>
        public IRaftClusterMember Leader
        {
            get => leader;
            private set
            {
                var oldLeader = Interlocked.Exchange(ref leader, value);
                if (!ReferenceEquals(oldLeader, value))
                    LeaderChanged?.Invoke(this, value);
            }
        }

        public virtual Task StartAsync(CancellationToken token)
        {
            //start node in Follower state
            consensusTerm.VolatileWrite(0L);
            state = new FollowerState(this, electionTimeout);
            return Task.CompletedTask;
        }

        public virtual async Task StopAsync(CancellationToken token)
        {
            transitionCancellation.Cancel(false);
            members.ForEach(member => member.CancelPendingRequests());
            leader = votedFor = null;
            switch (Interlocked.Exchange(ref state, null))
            {
                case FollowerState followerState:
                    followerState.Dispose();
                    return;
                case CandidateState candidateState:
                    await candidateState.StopVoting().ConfigureAwait(false);
                    candidateState.Dispose();
                    return;
                case LeaderState leaderState:
                    await leaderState.StopLeading(token).ConfigureAwait(false);
                    leader.Dispose();
                    return;
            }
        }

        private async Task EnsureFollowerState()
        {
            using (await transitionSync.Acquire(transitionCancellation.Token).ConfigureAwait(false))
            {
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
            }
        }

        private TMember FindMember<MemberId>(MemberId id, MemberMatcher<MemberId> matcher)
            => members.FirstOrDefault(member => matcher(member, id));

        protected async Task DoHeartbeat<MemberId>(MemberId sender, long senderTerm, MemberMatcher<MemberId> matcher)
        {
            var currentTerm = consensusTerm.AccumulateAndGet(senderTerm, Math.Max);
            if (currentTerm < senderTerm)
                return; //already replicated
            await EnsureFollowerState().ConfigureAwait(false);
            Leader = FindMember(sender, matcher);
        }

        protected async Task<bool> ReceiveEntries<MemberId>(MemberId sender, long senderTerm,
            MemberMatcher<MemberId> matcher, LogEntryId prevId, ILogEntry entry)
        {
            var currentTerm = consensusTerm.AccumulateAndGet(senderTerm, Math.Max);
            if (currentTerm > senderTerm || auditTrail is null || entry is null || auditTrail.Initial.Equals(entry.Id))
                return true; //already replicated
            await EnsureFollowerState().ConfigureAwait(false);
            Leader = FindMember(sender, matcher);
            return auditTrail.Contains(prevId) && await auditTrail.CommitAsync(entry).ConfigureAwait(false);
        }

        public Task ReplicateAsync(ILogEntry entry, CancellationToken token)
        {
            if (auditTrail is null)
                throw new NotSupportedException();
            else if (state is LeaderState leaderState)
                return leaderState.AppendEntriesAsync(entry, auditTrail, token);
            else
                throw new InvalidOperationException(ExceptionMessages.IsNotLeader);
        }

        private static TMember UpdateLastVote(TMember current, TMember candidate)
            => current ?? candidate;

        private bool Vote(TMember sender, LogEntryId? senderLastId)
        {
            if (sender is null)
                return false;
            else if (ReferenceEquals(sender, AtomicReference.AccumulateAndGet(ref votedFor, sender, UpdateLastVote)))
                return true;
            else if (auditTrail is null)
                return senderLastId is null;
            else if (senderLastId is null)
                return false;
            else
                return auditTrail.Contains(senderLastId.Value);
        }

        /// <summary>
        /// Votes for the new candidate.
        /// </summary>
        /// <param name="sender">The identifier of vote sender.</param>
        /// <param name="senderTerm">Term value provided by sender of the request.</param>
        /// <param name="matcher">The function allows to match member identifier with instance of <typeparamref name="TMember"/>.</param>
        /// <returns><see langword="true"/> if local node accepts new leader in the cluster; otherwise, <see langword="false"/>.</returns>
        protected async Task<bool> Vote<MemberId>(MemberId sender, long senderTerm, LogEntryId? senderLastId,
            MemberMatcher<MemberId> matcher)
        {
            var currentTerm = consensusTerm.AccumulateAndGet(senderTerm, Math.Max);
            if (currentTerm > senderTerm)
                return false;
            await EnsureFollowerState().ConfigureAwait(false);
            return Vote(FindMember(sender, matcher), senderLastId);
        }

        async Task<bool> ICluster.ResignAsync(CancellationToken token)
        {
            using (var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, transitionCancellation.Token))
            {
                using (await transitionSync.Acquire(transitionCancellation.Token).ConfigureAwait(false))
                    if (state is LeaderState leaderState)
                    {
                        await leaderState.StopLeading(tokenSource.Token).ConfigureAwait(false);
                        leaderState.Dispose();
                        state = new FollowerState(this, electionTimeout);
                        return true;
                    }

                var leader = Leader;
                return !(leader is null) && await leader.ResignAsync(tokenSource.Token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Revokes leadership of the local node.
        /// </summary>
        /// <returns><see langword="true"/>, if leadership is revoked successfully; otherwise, <see langword="false"/>.</returns>
        protected async Task<bool> Resign()
        {
            using (await transitionSync.Acquire(transitionCancellation.Token).ConfigureAwait(false))
                if (state is LeaderState leaderState)
                {
                    await leaderState.StopLeading(transitionCancellation.Token).ConfigureAwait(false);
                    leaderState.Dispose();
                    state = new FollowerState(this, electionTimeout);
                    return true;
                }
                else
                    return false;
        }

        async void IRaftStateMachine.MoveToFollowerState(bool randomizeTimeout)
        {
            using (await transitionSync.Acquire(transitionCancellation.Token).ConfigureAwait(false))
            {
                if (randomizeTimeout)
                    electionTimeout = electionTimeoutProvider.RandomTimeout();
                state = new FollowerState(this, electionTimeout);
            }
        }

        async void IRaftStateMachine.MoveToCandidateState()
        {
            using (await transitionSync.Acquire(transitionCancellation.Token).ConfigureAwait(false))
                if (state is FollowerState followerState)
                {
                    followerState.Dispose();
                    var newState = new CandidateState(this, absoluteMajority);
                    newState.StartVoting(electionTimeout, auditTrail);
                    state = newState;
                }
        }

        async void IRaftStateMachine.MoveToLeaderState(IRaftClusterMember leader)
        {
            using (await transitionSync.Acquire(transitionCancellation.Token).ConfigureAwait(false))
                if (state is CandidateState candidateState)
                {
                    candidateState.Dispose();
                    Leader = leader;
                }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                var members = Interlocked.Exchange(ref this.members, Array.Empty<TMember>());
                Dispose(members);
                members.Clear();
                transitionSync.Dispose();
                leader = votedFor = null;
            }
            base.Dispose(disposing);
        }
    }
}

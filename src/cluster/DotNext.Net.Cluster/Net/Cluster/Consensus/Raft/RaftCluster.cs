using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Collections.Concurrent;
    using Messaging;
    using Threading;

    /// <summary>
    /// Represents transport-independent implementation of Raft protocol.
    /// </summary>
    public abstract class RaftCluster<TMember> : Disposable, IRaftCluster
        where TMember : class, IRaftClusterMember
    {
        private sealed class ElectionTimeoutSource : Random
        {
            //recommended election timeout is between 150ms and 300ms
            internal int NextTimeout() => Next(150, 301);
        }

        /// <summary>
        /// Represents additional voting logic in the form of the delegate.
        /// </summary>
        /// <returns><see langword="true"/>, if the entire application votes successfully for the new cluster leader; otherwise, <see langword="false"/>.</returns>
        protected delegate bool VotingFunction();

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
            public TMember Remove()
            {
                node.List.Remove(node);
                var member = node.Value;
                node.Value = null;
                return member;
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

        /// <summary>
        /// Represents replicator.
        /// </summary>
        /// <param name="sender">The leader node that initiates cluster-wide replication.</param>
        /// <param name="entries">Log entries.</param>
        /// <returns><see langword="true"/>, if replication is accepted; otherwise, <see langword="false"/>.</returns>
        protected delegate Task<bool> Replicator(TMember sender, IMessage entries);

        private readonly struct MemberProcessingContext<T> : IDisposable
            where T : Task
        {
            internal readonly TMember Member;
            internal readonly T Task;

            internal MemberProcessingContext(TMember member, Func<TMember, T> asyncMethod)
                => Task = asyncMethod(Member = member);

            public void Dispose() => Task.Dispose();
        }

        private const int UnstartedState = 0;
        private const int FollowerState = 1;
        private const int CandidateState = 2;
        private const int LeaderState = 3;

        private volatile ICollection<TMember> members;
        
        private CancellationTokenSource electionCancellation;
        private AsyncLock transitionSync;  //used to synchronize state transitions
        private int state;
        private volatile TMember leader;
        private long consensusTerm;
        private readonly bool absoluteMajority;
        private readonly ElectionTimeoutSource electionTimeoutRandomizer;
        private volatile int electionTimeout;

        private BackgroundTask servingProcess;

        /// <summary>
        /// Initializes a new cluster manager for the local node.
        /// </summary>
        /// <param name="config">The configuration of the local node.</param>
        /// <param name="members">The collection of members that can be modified at construction stage.</param>
        protected RaftCluster(IClusterMemberConfiguration config, out MemberCollection members)
        {
            electionTimeout = (electionTimeoutRandomizer = new ElectionTimeoutSource()).NextTimeout();
            absoluteMajority = config.AbsoluteMajority;
            members = new MemberCollection(this.members = new LinkedList<TMember>());
            transitionSync = AsyncLock.Exclusive();
            state = UnstartedState;
        }

        protected virtual TimeSpan TransitionTimeout => TimeSpan.FromSeconds(20);

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

        private IReadOnlyCollection<IClusterMember> Members => state.VolatileRead() == UnstartedState ? Array.Empty<IClusterMember>() : (IReadOnlyCollection<IClusterMember>)members;

        int IReadOnlyCollection<IClusterMember>.Count => Members.Count;

        IEnumerator<IClusterMember> IEnumerable<IClusterMember>.GetEnumerator() => Members.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => Members.GetEnumerator();

        public long Term => consensusTerm.VolatileRead();
        
        public event ClusterLeaderChangedEventHandler LeaderChanged;
        public abstract event ClusterMemberStatusChanged MemberStatusChanged;

        IClusterMember ICluster.Leader => Leader;


        /// <summary>
        /// Gets leader of the cluster.
        /// </summary>
        public TMember Leader
        {
            get => leader;
            private set => LeaderChanged?.Invoke(this, leader = value);
        }

        async Task<bool> ICluster.ResignAsync(CancellationToken token)
        {
            bool result;
            using(var holder = await transitionSync.TryAcquire(TransitionTimeout, token).ConfigureAwait(false))
            {
                var leader = Leader;
                result = holder && (state.CompareAndSet(LeaderState, FollowerState) || leader != null && await leader.ResignAsync(token).ConfigureAwait(false));
                if(result)
                    Leader = null;
            }
            return result;
        }

        private async Task StartElection(CancellationToken token)
        {
            using(var holder = await transitionSync.TryAcquire(TimeSpan.Zero, token).ConfigureAwait(false))
            {
                if(!holder || !state.CompareAndSet(FollowerState, CandidateState))
                    return;
                Leader = null; //leader is not known, so erase it
                consensusTerm.IncrementAndGet();
                var voters = new LinkedList<MemberProcessingContext<Task<bool?>>>();
                //send vote request to all members in parallel
                Task<bool?> VoteMethod(TMember member) => member.VoteAsync(token);
                foreach (var member in members)
                    voters.AddLast(new MemberProcessingContext<Task<bool?>>(member, VoteMethod));
                //calculate votes
                var votes = 0;
                TMember localMember = null;
                for (var context = voters.First; !(context is null); context = context.Next)
                {
                    switch (await context.Value.Task.ConfigureAwait(false))
                    {
                        case true:
                            votes += 1;
                            break;
                        case false:
                            votes -= 1;
                            break;
                        default:
                            if (absoluteMajority)
                                votes -= 1;
                            break;
                    }
                    if(!context.Value.Member.IsRemote)
                        localMember = context.Value.Member;
                    context.Value.Dispose();
                    context.Value = default;
                }

                voters.Clear(); //help GC
                if (votes > 0 && localMember != null) //becomes a leader
                {
                    state.VolatileWrite(LeaderState);
                    Leader = localMember;
                }
                else    //no clear consensus, back to Follower state
                {
                    state.VolatileWrite(FollowerState);
                    electionTimeout = electionTimeoutRandomizer.NextTimeout();
                }
            }
        }

        private static bool IsCompleted(Task task) => task.Status == TaskStatus.RanToCompletion;

        private async Task ReplicateAsync(IMessage message, CancellationToken stoppingToken)
        {
            var tasks = new LinkedList<MemberProcessingContext<Task<bool>>>();

            Task<bool> AppendEntriesMethod(TMember member) =>
                member.AppendEntriesAsync(message, stoppingToken);

            //send requests in parallel
            foreach (var member in members)
                tasks.AddLast(new MemberProcessingContext<Task<bool>>(member, AppendEntriesMethod));
            var exceptions = new LinkedList<ConsensusProtocolException>();
            for (var current = tasks.First; !(current is null); current = current.Next)
                try
                {
                    if (!await current.Value.Task.ConfigureAwait(false))
                        exceptions.AddLast(new ReplicationRejectedException(current.Value.Member));
                }
                catch (Exception e)
                {
                    exceptions.AddLast(new MemberUnavailableException(current.Value.Member,
                        ExceptionMessages.UnavailableMember, e));
                }

            if (exceptions.Count > 0)
                throw new AggregateException(ExceptionMessages.ReplicationFailed, exceptions);
        }

        private async Task Serve(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                switch (state.VolatileRead())
                {
                    case FollowerState:
                        if(await Task.Delay(electionTimeout, Volatile.Read(ref electionCancellation).Token).ContinueWith(IsCompleted, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.NotOnFaulted))
                            await StartElection(stoppingToken).ConfigureAwait(false);
                        continue;
                    case LeaderState:
                        using(var holder = await transitionSync.TryAcquire(TransitionTimeout, stoppingToken))
                            if(holder && state.VolatileRead() == LeaderState)
                                await ReplicateAsync(default(IMessage), stoppingToken).ConfigureAwait(false);
                        continue;
                    default:
                        return;
                }
            }
        }

        public virtual Task StartAsync(CancellationToken token)
        {
            //start node in Follower state
            consensusTerm.VolatileWrite(0L);
            state.VolatileWrite(FollowerState);
            Volatile.Write(ref electionCancellation, new CancellationTokenSource());
            servingProcess = new BackgroundTask(Serve);
            return Task.CompletedTask;
        }

        public virtual async Task StopAsync(CancellationToken token)
        {
            var electionCancellation = Volatile.Read(ref this.electionCancellation);
            electionCancellation.Cancel();
            //stop serving process
            await servingProcess.Stop(token).ConfigureAwait(false);
            servingProcess = default;
            using(members.AcquireReadLock())
                members.ForEach(member => member.CancelPendingRequests());
            state.VolatileWrite(UnstartedState);
            leader = null;
            electionCancellation.Dispose();
        }

        protected async Task<bool> ReceiveEntries(TMember sender, long senderTerm, IMessage entries,
            Replicator replicator)
        {
            ResetElectionTimeout();
            bool result;
            using(var holder = await transitionSync.TryAcquire(TransitionTimeout))
                if(holder && consensusTerm.VolatileRead() <= senderTerm)
                {
                    var leader = Leader;
                    if (!sender.Equals(leader)) //leader node was changed
                        Leader = leader;
                    consensusTerm.VolatileWrite(senderTerm);
                    state.VolatileWrite(FollowerState); //new leader detected
                    result = await replicator(sender, entries).ConfigureAwait(false);
                }
                else
                    result = false;
            return result;
        }

        public async Task ReplicateAsync(MessageFactory entries, CancellationToken token)
        {
            using(var holder = await transitionSync.TryAcquire(TimeSpan.MaxValue, token).ConfigureAwait(false))
            {
                if(!holder)
                    return;
                else if(state.VolatileRead() != LeaderState)
                    throw new InvalidOperationException(ExceptionMessages.IsNotLeader);
                else 
                    await ReplicateAsync(entries(), token).ConfigureAwait(false);
            }
        }
        
        private void ResetElectionTimeout()
        {
            var tokenSource = Interlocked.Exchange(ref electionCancellation, new CancellationTokenSource());
            tokenSource.Cancel();
            tokenSource.Dispose();
        }

        /// <summary>
        /// Votes for the new candidate.
        /// </summary>
        /// <param name="senderTerm">Term value provided by sender of the request.</param>
        /// <param name="votingFunc">Extension point to add additional voting logic.</param>
        /// <returns><see langword="true"/> if local node accepts new leader in the cluster; otherwise, <see langword="false"/>.</returns>
        protected async Task<bool> Vote(long senderTerm, VotingFunction votingFunc = null)
        {
            ResetElectionTimeout();
            bool vote;
            using(var lockHolder = await transitionSync.TryAcquire(TimeSpan.Zero).ConfigureAwait(false))
            {
                vote = lockHolder && state.VolatileRead() == FollowerState && consensusTerm.VolatileRead() <= senderTerm && (votingFunc is null ? true : votingFunc());
                if(vote)
                    consensusTerm.VolatileWrite(senderTerm);
            }
            return vote;
        }

        /// <summary>
        /// Revokes leadership of the local node.
        /// </summary>
        /// <returns><see langword="true"/>, if leadership is revoked successfully; otherwise, <see langword="false"/>.</returns>
        protected async Task<bool> Resign()
        {
            bool result;
            using(var lockHolder = await transitionSync.TryAcquire(TimeSpan.Zero).ConfigureAwait(false))
            {
                result = lockHolder && state.VolatileRead() == LeaderState;
                if(result)
                {
                    state.VolatileWrite(FollowerState);
                    Leader = null;
                }
            }
            return result;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                servingProcess.Dispose();
                var members = Interlocked.Exchange(ref this.members, Array.Empty<TMember>());
                Dispose(members);
                members.Clear();
                transitionSync.Dispose();
                leader = null;
            }
            base.Dispose(disposing);
        }
    }
}

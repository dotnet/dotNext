using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Net.Cluster.Consensus.Raft.Http;

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

        private readonly LinkedList<TMember> members;
        
        private readonly AsyncManualResetEvent transitionSync;  //used to synchronize state transitions
        private int state;
        private volatile TMember leader, local;
        private long consensusTerm;
        private readonly bool absoluteMajority;
        private readonly TimeSpan electionTimeout;

        private BackgroundTask servingProcess;

        /// <summary>
        /// Initializes a new cluster manager for the local node.
        /// </summary>
        /// <param name="config">The configuration of the local node.</param>
        /// <param name="members">The collection of members that can be modified at construction stage.</param>
        protected RaftCluster(IClusterMemberConfiguration config, out MemberCollection members)
        {
            electionTimeout = config.ElectionTimeout;
            absoluteMajority = config.AbsoluteMajority;
            members = new MemberCollection(this.members = new LinkedList<TMember>());
            transitionSync = new AsyncManualResetEvent(false);
            state = UnstartedState;
        }

        /// <summary>
        /// Modifies collection of cluster members.
        /// </summary>
        /// <param name="mutator">The action that can be used to change set of cluster members.</param>
        protected void ChangeMembers(MemberCollectionMutator mutator)
        {
            using (members.AcquireWriteLock())
                mutator(new MemberCollection(members));
        }

        private IReadOnlyCollection<IClusterMember> Members => state.VolatileRead() == UnstartedState ? Array.Empty<IClusterMember>() : (IReadOnlyCollection<IClusterMember>)members;

        int IReadOnlyCollection<IClusterMember>.Count => Members.Count;

        IEnumerator<IClusterMember> IEnumerable<IClusterMember>.GetEnumerator() => Members.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => Members.GetEnumerator();

        public long Term => consensusTerm.VolatileRead();
        
        public event ClusterLeaderChangedEventHandler LeaderChanged;
        public abstract event ClusterMemberStatusChanged MemberStatusChanged;

        IClusterMember ICluster.Leader => Leader;
        IClusterMember ICluster.LocalMember => LocalMember;


        /// <summary>
        /// Gets leader of the cluster.
        /// </summary>
        public TMember Leader
        {
            get => leader;
            private set => LeaderChanged?.Invoke(this, leader = value);
        }

        /// <summary>
        /// Gets local cluster node.
        /// </summary>
        public TMember LocalMember
        {
            get => local;
            private set
            {
                if (!value.IsRemote)
                    local = value;
            }
        }

        async Task<bool> ICluster.ResignAsync(CancellationToken token)
        {
            var result = transitionSync.Set();
            if (result)
            {
                if (state == LeaderState)
                {
                    state = FollowerState;
                    Leader = null;
                }
                else
                {
                    var leader = Leader;
                    result = !(leader is null) && await leader.ResignAsync(token).ConfigureAwait(false);
                }
            }

            return result;
        }

        private async Task StartElection(CancellationToken token)
        {
            if (state > FollowerState)
                return;
            Leader = null; //leader is not known, so erase it
            //becomes a candidate
            state.VolatileWrite(CandidateState);
            consensusTerm.IncrementAndGet();
            var voters = new LinkedList<MemberProcessingContext<Task<bool?>>>();
            //send vote request to all members in parallel
            Task<bool?> VoteMethod(TMember member) => member.VoteAsync(token);
            using(members.AcquireReadLock())
                foreach (var member in members)
                    voters.AddLast(new MemberProcessingContext<Task<bool?>>(member, VoteMethod));
            //calculate votes
            var votes = 0;
            for (var context = voters.First; !(context is null); LocalMember = context.Value.Member, context = context.Next)
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
                context.Value.Dispose();
                context.Value = default;
            }

            voters.Clear(); //help GC
            if (votes > 0) //becomes a leader
            {
                state.VolatileWrite(LeaderState);
                Leader = LocalMember;
            }
            else
                state.VolatileWrite(FollowerState); //no clear consensus, back to Follower state
        }

        //election process can be started in this state
        private async Task ProcessFollowerState(CancellationToken stoppingToken)
        {
            using (var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken))
            {
                var timer = Task.Delay(electionTimeout, tokenSource.Token);
                var completedTask = await Task.WhenAny(transitionSync.Wait(tokenSource.Token), timer).ConfigureAwait(false);
                if (tokenSource.IsCancellationRequested)    //execution aborted
                    return;
                else if (ReferenceEquals(timer, completedTask) && transitionSync.Set()) //timeout happened
                    try
                    {
                        await StartElection(tokenSource.Token).ConfigureAwait(false);
                    }
                    finally
                    {
                        transitionSync.Reset();
                    }
                else
                    tokenSource.Cancel();   //ensure that Delay is destroyed
            }
        }

        private async Task ProcessLeaderState(IMessage message, CancellationToken stoppingToken)
        {
            var tasks = new LinkedList<MemberProcessingContext<Task<bool>>>();

            Task<bool> AppendEntriesMethod(TMember member) =>
                member.AppendEntriesAsync(message, stoppingToken);

            //send requests in parallel
            using(members.AcquireReadLock())
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

        //heartbeat broadcasting
        private async Task ProcessLeaderState(MessageFactory entries, CancellationToken stoppingToken)
        {
            transitionSync.Set();
            try
            {
                await ProcessLeaderState(entries?.Invoke(), stoppingToken).ConfigureAwait(false);
            }
            finally
            {
                transitionSync.Reset();
            }
        }

        private async Task Serve(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                switch (state.VolatileRead())
                {
                    case FollowerState:
                        await ProcessFollowerState(stoppingToken).ConfigureAwait(false);
                        continue;
                    case LeaderState:
                        await ProcessLeaderState(default(MessageFactory), stoppingToken).ConfigureAwait(false);
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
            transitionSync.Reset();
            servingProcess = new BackgroundTask(Serve);
            return Task.CompletedTask;
        }

        public virtual async Task StopAsync(CancellationToken token)
        {
            //stop serving process
            await servingProcess.Stop(token).ConfigureAwait(false);
            servingProcess = default;
            using(members.AcquireReadLock())
                members.ForEach(member => member.CancelPendingRequests());
            state.VolatileWrite(UnstartedState);
            local = leader = null;
        }

        protected async Task<bool> ReceiveEntries(TMember sender, long senderTerm, IMessage entries,
            Replicator replicator)
        {
            var result = transitionSync.Set();
            if (result)
                try
                {
                    if (consensusTerm.VolatileRead() <= senderTerm)
                        result = false;
                    else
                    {
                        var leader = Leader;
                        if (!sender.Equals(leader)) //leader node was changed
                            Leader = leader;
                        consensusTerm.VolatileWrite(senderTerm);
                        state.VolatileWrite(FollowerState); //new leader detected
                        result = await replicator(sender, entries).ConfigureAwait(false);
                    }
                }
                finally
                {
                    transitionSync.Reset();
                }

            return result;
        }

        public Task ReplicateAsync(MessageFactory entries)
            => state.VolatileRead() == LeaderState
                ? ProcessLeaderState(entries, CancellationToken.None)
                : throw new InvalidOperationException(ExceptionMessages.IsNotLeader);

        /// <summary>
        /// Votes for the new candidate.
        /// </summary>
        /// <param name="senderTerm">Term value provided by sender of the request.</param>
        /// <returns><see langword="true"/> if local node accepts new leader in the cluster; otherwise, <see langword="false"/>.</returns>
        protected bool Vote(long senderTerm)
        {
            var vote = transitionSync.Set();
            if (vote)
            {
                if (state.VolatileRead() > FollowerState || consensusTerm.VolatileRead() > senderTerm)
                    vote = false;
                else
                    consensusTerm.VolatileWrite(senderTerm);
                transitionSync.Reset();
            }

            return vote;
        }

        /// <summary>
        /// Revokes leadership of the local node.
        /// </summary>
        /// <returns><see langword="true"/>, if leadership is revoked successfully; otherwise, <see langword="false"/>.</returns>
        protected bool Resign()
        {
            if (state.CompareAndSet(LeaderState, FollowerState))
            {
                Leader = null;
                return true;
            }
            return false;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                servingProcess.Dispose();
                for (var current = members.First; !(current is null); current = current.Next)
                {
                    current.Value.Dispose();
                    current.Value = null;
                }
                members.Clear();
                transitionSync.Dispose();
                local = leader = null;
            }
            base.Dispose(disposing);
        }
    }
}

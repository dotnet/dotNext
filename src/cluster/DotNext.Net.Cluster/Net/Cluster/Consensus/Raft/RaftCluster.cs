using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Collections.Concurrent;
using DotNext.Threading;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Represents transport-independent implementation of Raft protocol.
    /// </summary>
    public abstract class RaftCluster : Disposable, IRaftCluster, IClusterMemberIdentity
    {
        private const int UnstartedState = 0;
        private const int FollowerState = 1;
        private const int CandidateState = 2;
        private const int LeaderState = 3;

        private readonly CopyOnWriteList<IRaftClusterMember> members;
        
        private readonly AsyncManualResetEvent electionTimeoutRefresher;
        private readonly OutboundMessageQueue messages;
        private int state;
        private volatile IClusterMember leader, local;
        private readonly AsyncExclusiveLock monitor;
        private long consensusTerm;
        private readonly string name;
        private readonly Guid id;
        private readonly bool absoluteMajority;
        private readonly TimeSpan electionTimeout, messageProcessingTimeout;

        private BackgroundTask servingProcess;

        /// <summary>
        /// Initializes a new cluster manager for the local node.
        /// </summary>
        /// <param name="config">The configuration of the local node.</param>
        protected RaftCluster(IRaftClusterMemberFactory config)
        {
            name = config.MemberName;
            id = Guid.NewGuid();
            electionTimeout = config.ElectionTimeout;
            messageProcessingTimeout = config.MessageProcessingTimeout;
            absoluteMajority = config.AbsoluteMajority;
            members = new CopyOnWriteList<IRaftClusterMember>(config.CreateMembers(this));
            electionTimeoutRefresher = new AsyncManualResetEvent(false);
            state = UnstartedState;
            monitor = new AsyncExclusiveLock();
        }

        public long Term => consensusTerm.VolatileRead();

        bool IRaftCluster.IsLeader(IRaftClusterMember member) => ReferenceEquals(Leader, member);

        string IClusterMemberIdentity.Name => name;
        Guid IClusterMemberIdentity.Id => id;

        IReadOnlyCollection<IClusterMember> ICluster.Members
            => state.VolatileRead() == UnstartedState ? Array.Empty<IClusterMember>() : (IReadOnlyCollection<IClusterMember>)members;

        public event ClusterLeaderChangedEventHandler LeaderChanged;
        public event ClusterMemberStatusChanged MemberStatusChanged;
        public event MessageHandler MessageReceived;

        private RequestContext Context => new RequestContext(MemberStatusChanged);

        /// <summary>
        /// Gets leader of the cluster.
        /// </summary>
        public IClusterMember Leader
        {
            get => leader;
            private set => LeaderChanged?.Invoke(this, leader = value);
        }

        /// <summary>
        /// Gets local cluster node.
        /// </summary>
        public IClusterMember LocalMember
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
            using (await monitor.AcquireLock(token).ConfigureAwait(false))
            {
                if (state == LeaderState)
                {
                    state = FollowerState;
                    Leader = null;
                    return true;
                }
                else if (leader is IRaftClusterMember member)
                    return await member.Resign(Context, token).ConfigureAwait(false);
                else
                    return false;
            }
        }

        public async Task PostMessageAsync(IMessage message, CancellationToken token)
        {
            Task messageTask;
            using(await monitor.AcquireLock(token).ConfigureAwait(false))
                messageTask = messages.Enqueue(message, ref token);
            await messageTask.ConfigureAwait(false);
        }

        private async Task StartElection(CancellationToken token)
        {
            using (await monitor.AcquireLock(token).ConfigureAwait(false))
            {
                if (state > FollowerState || electionTimeoutRefresher.IsSet)
                {
                    electionTimeoutRefresher.Reset();
                    return;
                }
                Leader = null;  //leader is not known, so erase it
                //becomes a candidate
                state.VolatileWrite(CandidateState);
                consensusTerm.IncrementAndGet();
                var voters = new LinkedList<(IRaftClusterMember member, Task<bool?> task)>();
                //send vote request to all members in parallel
                foreach (var member in (IEnumerable<IRaftClusterMember>)members)
                    voters.AddLast((member, member.Vote(Context, token)));
                //calculate votes
                var votes = 0;
                for (var member = voters.First; !(member is null); LocalMember = member.Value.member, member = member.Next)
                {
                    switch (await member.Value.task.ConfigureAwait(false))
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
                    member.Value.task.Dispose();
                }

                voters.Clear(); //help GC
                if (votes > 0)  //becomes a leader
                {
                    state.VolatileWrite(LeaderState);
                    Leader = LocalMember;
                }
                else
                    state.VolatileWrite(FollowerState); //no clear consensus, back to Follower state
            }
        }

        //election process can be started in this state
        private async Task ProcessFollowerState(CancellationToken stoppingToken)
        {
            using (var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken))
            {
                var timer = Task.Delay(electionTimeout, tokenSource.Token);
                var completedTask = await Task.WhenAny(electionTimeoutRefresher.Wait(tokenSource.Token), timer).ConfigureAwait(false);
                if (tokenSource.IsCancellationRequested)    //execution aborted
                    return;
                else if (ReferenceEquals(timer, completedTask)) //timeout happened
                    await StartElection(tokenSource.Token).ConfigureAwait(false);
                else
                {
                    tokenSource.Cancel();   //ensure that Delay is destroyed
                    electionTimeoutRefresher.Reset();
                }
            }
        }

        //heartbeat broadcasting
        private async Task ProcessLeaderState(CancellationToken stoppingToken)
        {
            using (await monitor.AcquireLock(stoppingToken).ConfigureAwait(false))
            {

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
                        await ProcessLeaderState(stoppingToken).ConfigureAwait(false);
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
            electionTimeoutRefresher.Reset();
            servingProcess = new BackgroundTask(Serve);
            return Task.CompletedTask;
        }

        public virtual async Task StopAsync(CancellationToken token)
        {
            //stop serving process
            await servingProcess.Stop(token).ConfigureAwait(false);
            servingProcess = default;
            foreach (var member in (IEnumerable<IRaftClusterMember>)members)
                member.CancelPendingRequests();
            state.VolatileWrite(UnstartedState);
            local = leader = null;
        }

        protected async Task<bool> ReceiveMessage(IClusterMemberIdentity sender, long senderTerm, IMessage message)
        {
            if (sender.Id == id)
                return true;
            var monitorLock = await monitor.AcquireLock(messageProcessingTimeout).ConfigureAwait(false);
            try
            {
                if (consensusTerm.VolatileRead() <= senderTerm)
                     return false;
                var leader = Leader;
                if(!sender.Represents(leader))  //leader node was changed
                {
                    leader = members.Find(sender.Represents);
                    if(leader is null)   //sender is not in member list, ignores message
                        return false;
                    Leader = leader;
                }
                consensusTerm.VolatileWrite(senderTerm);
                state.VolatileWrite(FollowerState); //new leader detected
                MessageReceived?.Invoke(leader, message);
                return true;
            }
            finally
            {
                electionTimeoutRefresher.Set();
                monitorLock.Dispose();
            }
        }

        /// <summary>
        /// Votes for the new candidate.
        /// </summary>
        /// <param name="sender">The sender of the vote request.</param>
        /// <param name="senderTerm">Term value provided by sender of the request.</param>
        /// <returns><see langword="true"/> if local node accepts new leader in the cluster; otherwise, <see langword="false"/>.</returns>
        protected async Task<bool> Vote(IClusterMemberIdentity sender, long senderTerm)
        {
            if (sender.Id == id) //sender node and receiver are same, fast response without synchronization
                return true;
            var monitorLock = await monitor.AcquireLock(messageProcessingTimeout).ConfigureAwait(false);
            try
            {
                if (state.VolatileRead() > FollowerState || consensusTerm.VolatileRead() > senderTerm)
                    return false;
                else
                {
                    consensusTerm.VolatileWrite(senderTerm);
                    return true;
                }
            }
            finally
            {
                electionTimeoutRefresher.Set();
                monitorLock.Dispose();
            }
        }

        /// <summary>
        /// Revokes leadership of the local node.
        /// </summary>
        /// <returns><see langword="true"/>, if leadership is revoked successfully; otherwise, <see langword="false"/>.</returns>
        protected async Task<bool> Resign()
        {
            using (await monitor.AcquireLock(messageProcessingTimeout).ConfigureAwait(false))
                if (state == LeaderState)
                {
                    state = FollowerState;
                    Leader = null;
                    return true;
                }
                else
                    return false;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                servingProcess.Dispose();
                members.Clear(member => member.Dispose());
                Dispose(monitor, electionTimeoutRefresher);
                messages.CancelAll();
                local = leader = null;
            }
        }
    }
}

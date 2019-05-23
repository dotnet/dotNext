using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal sealed class RaftCluster : BackgroundService, ICluster, IMiddleware, ILocalMember
    {
        private const int UnstartedState = 0;
        private const int FollowerState = 1;
        private const int CandidateState = 2;
        private const int LeaderState = 3;

        private long consensusTerm;
        private readonly LinkedList<RaftClusterMember> members;
        private readonly TimeSpan electionTimeout;
        private readonly AsyncAutoResetEvent electionTimeoutRefresher;
        private int state;
        private volatile IClusterMember leader, local;
        private readonly string name;
        private readonly Guid id;
        private readonly AsyncExclusiveLock monitor;
        private ClusterStatus status;
        private readonly bool absoluteMajority;

        private RaftCluster(ClusterMemberConfiguration config)
        {
            absoluteMajority = config.AbsoluteMajority;
            id = Guid.NewGuid();
            name = config.MemberName;
            members = new LinkedList<RaftClusterMember>();
            electionTimeoutRefresher = new AsyncAutoResetEvent(false);
            electionTimeout = config.ElectionTimeout;
            state = UnstartedState;
            foreach (var memberUri in config.Members)
                members.AddLast(new RaftClusterMember(memberUri, config.ResourcePath));
            monitor = new AsyncExclusiveLock();
        }

        internal RaftCluster(IOptions<ClusterMemberConfiguration> config)
            : this(config.Value)
        {
        }

        string ILocalMember.Name => name;
        Guid ILocalMember.Id => id;
        long ILocalMember.Term => consensusTerm.VolatileRead();

        public ClusterStatus Status
        {
            get => status;
            private set
            {
                var oldStatus = status;
                var newStatus = status = value;
                if (oldStatus != newStatus)
                    StatusChanged?.Invoke(this, oldStatus, newStatus);
            }
        }

        IReadOnlyCollection<IClusterMember> ICluster.Members 
            => state.VolatileRead() == UnstartedState ? Array.Empty<IClusterMember>() : (IReadOnlyCollection<IClusterMember>)members;

        public IClusterMember Leader
        {
            get => leader;
            private set => LeaderChanged?.Invoke(this, leader = value);
        }

        public IClusterMember LocalMember
        {
            get => local;
            private set
            {
                if (!value.IsRemote)
                    local = value;
            }
        }

        public event ClusterLeaderChangedEventHandler LeaderChanged;
        public event ClusterStatusChangedEventHandler StatusChanged;
        public event ClusterMemberStatusChanged MemberStatusChanged;
        public event MessageHandler MessageReceived;

        void ICluster.Resign()
        {

        }

        public Task EnqueueMessageAsync(IMessage message, TimeSpan timeout, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        private async Task StartElection(CancellationToken token)
        {
            using (await monitor.AcquireLock(token).ConfigureAwait(false))
            {
                if (state > FollowerState || electionTimeoutRefresher.IsSet)
                    return;
                //becomes a candidate
                state.VolatileWrite(CandidateState);
                consensusTerm += 1L;
                var voters = new LinkedList<(RaftClusterMember member, Task<bool?> task)>();
                //send vote request to all members in parallel
                for (var member = members.First; !(member is null); member = member.Next)
                    voters.AddLast((member.Value, member.Value.Vote(this, MemberStatusChanged, token)));
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
                    Status = ClusterStatus.Operating;
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
                    tokenSource.Cancel();   //ensure that Delay or AutoResetEvent is destroyed
            }
        }

        //heartbeat broadcasting
        private async Task ProcessLeaderState(CancellationToken stoppingToken)
        {

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while(!stoppingToken.IsCancellationRequested)
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

        public override Task StartAsync(CancellationToken token)
        {
            //start node in Follower state
            consensusTerm = 0L;
            Status = ClusterStatus.NoConsensus;
            state.VolatileWrite(FollowerState);
            electionTimeoutRefresher.Reset();
            return base.StartAsync(token);
        }

        public override async Task StopAsync(CancellationToken token)
        {
            await base.StopAsync(token).ConfigureAwait(false);  //stop background task
            foreach(var member in members)
                member.CancelPendingRequests();
            state.VolatileWrite(UnstartedState);
            local = null;
        }

        private async Task ReceiveVoteRequest(RaftHttpMessage request, HttpResponse response)
        {
            bool vote;
            using (await monitor.AcquireLock(CancellationToken.None).ConfigureAwait(false))
            {
                if (consensusTerm > request.ConsensusTerm || state.VolatileRead() > FollowerState)
                    vote = false;
                else
                {
                    consensusTerm = request.ConsensusTerm;
                    vote = true;
                }

                electionTimeoutRefresher.Set();
            }

            await RequestVoteMessage.CreateResponse(response, id, name, vote).ConfigureAwait(false);
        }

        public Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            switch (RaftHttpMessage.GetMessageType(context.Request))
            {
                case RequestVoteMessage.MessageType:
                    return ReceiveVoteRequest(new RequestVoteMessage(context.Request),  context.Response);
                default:
                    return next(context);
            }
        }

        public override void Dispose()
        {
            for(var current = members.First; !(current is null); current = current.Next)
            {
                current.Value.Dispose();
                current.Value = null;
                current.List.Remove(current);
            }
            monitor.Dispose();
            electionTimeoutRefresher.Dispose();
            local = leader = null;
            base.Dispose();
        }
    }
}

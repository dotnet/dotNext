using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using AsyncAutoResetEvent = Threading.AsyncAutoResetEvent;
    using static Threading.AtomicInt32;

    internal sealed class RaftCluster : BackgroundService, IHostedService, ICluster
    {
        private const int Unstarted = 0;
        private const int FollowerStatus = 1;
        private const int CandidateStatus = 2;
        private const int LeaderStatus = 3;

        private const string RaftTermHeader = "X-Raft-Term";

        private long consensusTerm;
        private readonly LinkedList<RaftClusterMember> members;
        private readonly TimeSpan electionTimeout;
        private readonly AsyncAutoResetEvent electionTimeoutRefresher;
        private readonly CancellationTokenSource stoppingTokenSource;
        private int nodeStatus;
        private volatile IClusterMember leader;
        private readonly Guid id;
        private readonly string name;
        private RaftClusterMember local;

        private RaftCluster(ClusterMemberConfiguration config)
        {
            id = Guid.NewGuid();
            name = config.MemberName;
            members = new LinkedList<RaftClusterMember>();
            electionTimeoutRefresher = new AsyncAutoResetEvent(false);
            stoppingTokenSource = new CancellationTokenSource();
            electionTimeout = config.ElectionTimeout;
            nodeStatus = Unstarted;
            foreach (var memberUri in config.Members)
                members.AddLast(new RaftClusterMember(id, memberUri));
        }

        internal RaftCluster(IOptions<ClusterMemberConfiguration> config)
            : this(config.Value)
        {
        }

        public ClusterStatus Status { get; private set; }

        IReadOnlyCollection<IClusterMember> ICluster.Members 
            => nodeStatus.VolatileRead() == Unstarted ? Array.Empty<IClusterMember>() : (IReadOnlyCollection<IClusterMember>)members;

        IClusterMember ICluster.Leader => leader;
        IClusterMember ICluster.LocalMember => local;
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

        private void UpdateLocal(LinkedListNode<RaftClusterMember> member)
        {
            if(member.Value.IsLocal && local is null)
                local = member.Value;
        }

        private async Task StartElection(CancellationToken token)
        {
            //becomes a candidate
            if (!token.IsCancellationRequested && nodeStatus.CompareAndSet(FollowerStatus, CandidateStatus))
            {
                var voters = new LinkedList<(RaftClusterMember, Task<bool?>)>();
                var votes = 0;
                //send vote request to all members in parallel
                for(var member = members.First; !(member is null); member = member.Next)
                    voters.AddLast((member.Value, member.Value.Vote(token)));
                
                nodeStatus.VolatileWrite(LeaderStatus);
            }
        }

        private void RefreshElectionTimeout() => electionTimeoutRefresher.Set();
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while(!stoppingToken.IsCancellationRequested)
            {
                using(var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken))
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
        }

        public override Task StartAsync(CancellationToken token)
        {
            //start node in Follower state
            consensusTerm = 0L;
            Status = ClusterStatus.NoConsensus;
            nodeStatus.VolatileWrite(FollowerStatus);
            electionTimeoutRefresher.Reset();
            return base.StartAsync(token);
        }

        public override async Task StopAsync(CancellationToken token)
        {
            await base.StopAsync(token);  //stop backgroud task
            foreach(var member in members)
                member.CancelPendingRequests();
            nodeStatus.VolatileWrite(Unstarted);
            local = null;
        }

        public override void Dispose()
        {
            for(var current = members.First; !(current is null); current = current.Next)
            {
                current.Value.Dispose();
                current.Value = null;
                current.List.Remove(current);
            }
            local = null;
            base.Dispose();
        }
    }
}

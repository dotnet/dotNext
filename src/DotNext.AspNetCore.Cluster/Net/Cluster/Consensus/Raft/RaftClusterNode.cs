using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal sealed class RaftClusterNode : BackgroundService, IRaftClusterNode, IHostedService
    {
        private const int FollowerStatus = (int)ClusterNodeStatus.Follower;
        private const int CandidateStatus = (int) ClusterNodeStatus.Candidate;
        private const int LeaderStatus = (int) ClusterNodeStatus.Leader;

        private const string RaftTermHeader = "X-Raft-Term";

        private long consensusTerm;
        private readonly LinkedList<IRaftClusterMember> members;
        private readonly TimeSpan electionTimeout;
        private readonly AsyncAutoResetEvent electionTimeoutRefresher;
        private readonly CancellationTokenSource stoppingTokenSource;
        private int nodeStatus;

        private RaftClusterNode(ClusterMemberConfiguration config)
        {
            members = new LinkedList<IRaftClusterMember>();
            Id = Guid.NewGuid();
            Name = config.MemberName;
            electionTimeoutRefresher = new AsyncAutoResetEvent(false);
            stoppingTokenSource = new CancellationTokenSource();
            electionTimeout = config.ElectionTimeout;
            foreach (var memberUri in config.Members)
                members.AddLast(new RemoteClusterMember(memberUri));
        }

        internal RaftClusterNode(IOptions<ClusterMemberConfiguration> config)
            : this(config.Value)
        {
        }

        ClusterNodeStatus IRaftClusterNode.NodeStatus => (ClusterNodeStatus) nodeStatus.VolatileRead();

        public IPEndPoint Endpoint { get; }
        bool IClusterMember.IsLeader => nodeStatus.VolatileRead() == LeaderStatus;
        bool IClusterMember.IsRemote => false;
        public Guid Id { get; }
        public string Name { get; }
        public bool IsAvailable { get; }

        public ClusterStatus ClusterStatus { get; private set; }

        IReadOnlyCollection<IClusterMember> IClusterNode.Members => members;

        public IClusterMember Leader { get; }
        public event LeaderChangedEventHandler LeaderChanged;
        public event ClusterStatusChangedEventHandler ClusterStatusChanged;
        public event ClusterMemberStatusChanged MemberStatusChanged;
        public event MessageHandler MessageReceived;

        public void Resign()
        {
            throw new NotImplementedException();
        }

        public Task<bool> WaitForLeadershipAsync(TimeSpan timeout, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public Task EnqueueMessageAsync(IMessage message, TimeSpan timeout, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        private async Task StartElection(CancellationToken token)
        {
            //becomes a candidate
            if (nodeStatus.CompareAndSet(FollowerStatus, CandidateStatus))
                using (await members.AcquireUpgradeableReadLockAsync(token).ConfigureAwait(false))
                {
                    var voters = 0;
                    //send vote request to all members
                    foreach (var member in members)
                        switch(await member.Vote(Id, token))
                        {
                            case null:
                                continue;
                            case true:
                                voters += 1;
                                goto default;
                            default:
                                voters -= 1;
                                break;
                        }
                    nodeStatus.VolatileWrite(LeaderStatus);
                }
        }

        private async void StartTracking(object source)
        {
            using (var tokenSource = source as CancellationTokenSource ?? new CancellationTokenSource())
            {
                var timer = Task.Delay(electionTimeout, tokenSource.Token);
                var completedTask = await Task.WhenAny(electionTimeoutRefresher.Wait(tokenSource.Token), timer)
                    .ConfigureAwait(false);
                if (tokenSource.IsCancellationRequested)    //was cancelled at startup
                    return;
                else if (ReferenceEquals(timer, completedTask)) //timeout happened
                    await StartElection(tokenSource.Token).ConfigureAwait(false);
                else if (completedTask.IsCanceled)  
                    return;
                tokenSource.Cancel(); //ensure that Delay or AutoResetEvent is destroyed
            }
                
            //continue polling
            ThreadPool.QueueUserWorkItem(StartTracking);
        }

        private void RefreshElectionTimeout() => electionTimeoutRefresher.Set();

        private void DetectMe(CancellationToken token)
        {
            var detected = false;
            foreach(var endpoint in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners())
                for(var member = members.First; !(member is null); member = member.Next, token.ThrowIfCancellationRequested())
                    if(member.Value.Endpoint.Equals(endpoint))
                    {
                        member.Value = this;
                        detected = true;
                        break;
                    }
            if(!detected)
                throw new InvalidOperationException(ExceptionMessages.MissingNodeEndpoint);
        }

        public override Task StartAsync(CancellationToken token)
        {
            //start node in Follower state
            consensusTerm = 0L;
            ClusterStatus = ClusterStatus.NoConsensus;
            nodeStatus.VolatileWrite(FollowerStatus);
            electionTimeoutRefresher.Reset();
            //detect this node using network information
            DetectMe(token);
            return base.StartAsync(token);
        }

        public override async Task StopAsync(CancellationToken token)
        {
            await base.StopAsync(token);  //stop backgroud task
            foreach(var member in members)
                member.CancelPendingRequests();
        }

        public override void Dispose()
        {
            for(var current = members.First; !(current is null); current = current.Next)
                if(current.Value.IsRemote)
                {
                    current.Value.Dispose();
                    current.Value = null;
                    current.List.Remove(current);
                }
            base.Dispose();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal sealed class RaftClusterNode : Disposable, IRaftClusterNode, IHostedService
    {
        private const int FollowerStatus = (int)ClusterNodeStatus.Follower;
        private const int CandidateStatus = (int) ClusterNodeStatus.Candidate;
        private const int LeaderStatus = (int) ClusterNodeStatus.Leader;

        private const string RaftTermHeader = "X-Raft-Term";

        private long consensusTerm;
        private readonly LinkedList<RemoteClusterMember> members;
        private readonly TimeSpan electionTimeout;
        private readonly AsyncAutoResetEvent electionTimeoutRefresher;
        private int nodeStatus;

        internal RaftClusterNode(ClusterMemberConfiguration config)
        {
            Id = Guid.NewGuid();
            electionTimeoutRefresher = new AsyncAutoResetEvent(false);
            foreach (var memberUri in config.Members)
                members.AddLast(new RemoteClusterMember(memberUri));
        }

        ClusterNodeStatus IRaftClusterNode.NodeStatus => (ClusterNodeStatus) nodeStatus.VolatileRead();

        public IPEndPoint Endpoint { get; }
        bool IClusterMember.IsLeader => nodeStatus.VolatileRead() == LeaderStatus;
        bool IClusterMember.IsRemote => false;
        public Guid Id { get; }
        public string Name { get; private set; }
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

        Task IHostedService.StartAsync(CancellationToken token)
        {
            //start node in Follower state
            consensusTerm = 0L;
            ClusterStatus = ClusterStatus.NoConsensus;
            nodeStatus.VolatileWrite(FollowerStatus);
            electionTimeoutRefresher.Reset();
            ThreadPool.QueueUserWorkItem(StartTracking, CancellationTokenSource.CreateLinkedTokenSource(token));
            return Task.CompletedTask;
        }

        private void RemoveMembers()
        {
            for (var current = members.First; !(current is null); current = current.Next)
            {
                current.Value.Dispose();
                members.Remove(current);
                current.Value = null;
            }
        }

        Task IHostedService.StopAsync(CancellationToken token)
        {
            RemoveMembers();
            return Task.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                RemoveMembers();
            }
            base.Dispose(disposing);
        }
    }
}

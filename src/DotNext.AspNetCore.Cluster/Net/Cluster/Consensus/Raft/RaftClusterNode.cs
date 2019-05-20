using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal sealed class RaftClusterNode : IRaftClusterNode
    {
        private protected const string RaftTermHeader = "X-Raft-Term";

        private long consensusTerm;

        public IPEndPoint Endpoint { get; }
        public bool IsLeader { get; }
        public bool IsRemote { get; }
        public Guid Id { get; }
        public string Name { get; }
        public bool IsAvailable { get; }
        public ClusterStatus ClusterStatus { get; }
        public IReadOnlyCollection<IClusterMember> Members { get; }
        public IClusterMember Leader { get; }
        public event LeaderChangedEventHandler LeaderChanged;
        public event ClusterStatusChangedEventHandler ClusterStatusChanged;

        public Task Resign()
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

        public ClusterNodeStatus NodeStatus { get; }
    }
}

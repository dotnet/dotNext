using DotNext.Net.Cluster;
using DotNext.Net.Cluster.Consensus.Raft;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RaftNode
{
    internal sealed class ClusterConfigurator : IRaftClusterConfigurator
    {
        private static void LeaderChanged(ICluster cluster, IClusterMember leader)
        {
            Debug.Assert(cluster is IRaftCluster);
            var term = ((IRaftCluster)cluster).Term;
            var timeout = ((IRaftCluster)cluster).ElectionTimeout;
            Console.WriteLine(leader is null
                ? "Consensus cannot be reached"
                : $"New cluster leader is elected. Leader address is {leader.Endpoint}");
            Console.WriteLine($"Term of local cluster member is {term}. Election timeout {timeout}");
        }

        public void Initialize(IRaftCluster cluster, IDictionary<string, string> metadata)
        {
            cluster.LeaderChanged += LeaderChanged;
        }

        public void Shutdown(IRaftCluster cluster)
        {
            cluster.LeaderChanged -= LeaderChanged;
        }
    }
}

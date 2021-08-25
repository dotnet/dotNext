using System;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Describes how a new cluster node should be bootstrapped.
    /// </summary>
    public enum ClusterMemberBootstrap
    {
        /// <summary>
        /// The node was shutted down accidentally so no need to join the cluster is a special way. 
        /// </summary>
        Recovery = 0,

        /// <summary>
        /// The node is started in StandBy mode and wait for confirmation from the leader node.
        /// </summary>
        /// <remarks>
        /// Such a node doesn't participate in leader election, voting and reject any client request
        /// until the node will be added using AddServer Raft command.
        /// </remarks>
        Announcement,

        /// <summary>
        /// The first node is started in cluster so it adds itself to the log in committed state.
        /// </summary>
        ColdStart,
    }
}

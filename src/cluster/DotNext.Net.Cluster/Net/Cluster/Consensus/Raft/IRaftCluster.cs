using System;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Replication;
    using Messaging;

    /// <summary>
    /// Represents cluster of nodes coordinated using Raft consensus protocol.
    /// </summary>
    public interface IRaftCluster : ICluster
    {
        /// <summary>
        /// Gets term number used by Raft algorithm to check the consistency of the cluster.
        /// </summary>
        long Term { get; }

        /// <summary>
        /// Replicates cluster members.
        /// </summary>
        /// <param name="entries">The message containing log entries to be sent to other cluster members.</param>
        /// <returns>The task representing asynchronous execution of replication.</returns>
        /// <exception cref="AggregateException">Unable to replicate one or more cluster nodes. You can analyze inner exceptions which are derive from <see cref="ConsensusProtocolException"/> or <see cref="ReplicationException"/>.</exception>
        /// <exception cref="InvalidOperationException">The caller application is not a leader node.</exception>
        Task ReplicateAsync(MessageFactory entries);
    }
}
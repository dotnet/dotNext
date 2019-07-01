using System;

namespace DotNext.Net.Cluster.Consensus.Raft
{
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
        /// Gets election timeout used by local cluster member.
        /// </summary>
        TimeSpan ElectionTimeout { get; }

        /// <summary>
        /// Setup audit trail for the cluster.
        /// </summary>
        /// <exception cref="InvalidOperationException">Audit trail is already defined for this instance.</exception>
        IPersistentState AuditTrail { set; }
    }
}
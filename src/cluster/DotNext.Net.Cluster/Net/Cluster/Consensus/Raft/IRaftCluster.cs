using System;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO.Log;
    using Replication;

    /// <summary>
    /// Represents cluster of nodes coordinated using Raft consensus protocol.
    /// </summary>
    public interface IRaftCluster : IReplicationCluster<IRaftLogEntry>
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
        /// Establishes metrics collector.
        /// </summary>
        MetricsCollector Metrics { set; }

        /// <summary>
        /// Defines persistent state for the Raft-based cluster.
        /// </summary>
        new IPersistentState AuditTrail { get; set; }

        /// <inheritdoc/>
        IAuditTrail<IRaftLogEntry> IReplicationCluster<IRaftLogEntry>.AuditTrail => AuditTrail;
    }
}
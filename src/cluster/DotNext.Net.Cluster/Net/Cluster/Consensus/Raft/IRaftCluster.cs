using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Replication;

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
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The task representing asynchronous execution of replication.</returns>
        /// <exception cref="AggregateException">Unable to replicate one or more cluster nodes. You can analyze inner exceptions which are derive from <see cref="ConsensusProtocolException"/> or <see cref="ReplicationException"/>.</exception>
        /// <exception cref="InvalidOperationException">The caller application is not a leader node.</exception>
        /// <exception cref="NotSupportedException">Audit trail is not defined for this instance.</exception>
        Task ReplicateAsync(ILogEntry<LogEntryId> entries, CancellationToken token = default);

        /// <summary>
        /// Setup audit trail for the cluster.
        /// </summary>
        /// <exception cref="InvalidOperationException">Audit trail is already defined for this instance.</exception>
        IPersistentState AuditTrail { set; }
    }
}
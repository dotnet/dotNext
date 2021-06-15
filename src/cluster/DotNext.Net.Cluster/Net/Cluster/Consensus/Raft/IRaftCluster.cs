using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO.Log;
    using Replication;
    using Timeout = Threading.Timeout;

    /// <summary>
    /// Represents cluster of nodes coordinated using Raft consensus protocol.
    /// </summary>
    public interface IRaftCluster : IReplicationCluster<IRaftLogEntry>
    {
        /// <summary>
        /// Gets term number used by Raft algorithm to check the consistency of the cluster.
        /// </summary>
        long Term => AuditTrail.Term;

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

        private async Task<bool> ReplicateAsync<TEntryImpl>(TEntryImpl entry, Timeout timeout, CancellationToken token)
            where TEntryImpl : notnull, IRaftLogEntry
        {
            var log = AuditTrail;

            // 1 - append entry to the log
            var index = await log.AppendAsync(entry, token).ConfigureAwait(false);
            timeout.ThrowIfExpired(out var remaining);

            // 2 - force replication
            if (await ForceReplicationAsync(remaining, token).ConfigureAwait(false))
            {
                timeout.ThrowIfExpired(out remaining);
            }
            else
            {
                return false;
            }

            // 3 - wait for commit
            if (!await log.WaitForCommitAsync(index, remaining, token).ConfigureAwait(false))
                throw new TimeoutException();

            return Term == entry.Term;
        }

        /// <inheritdoc />
        Task<bool> IReplicationCluster<IRaftLogEntry>.ReplicateAsync<TEntryImpl>(TEntryImpl entry, TimeSpan timeout, CancellationToken token = default)
            => ReplicateAsync(entry, new Timeout(timeout), token);
    }
}
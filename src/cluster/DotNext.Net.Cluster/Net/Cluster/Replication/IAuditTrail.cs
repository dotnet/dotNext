using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Replication
{
    using  Messaging;

    /// <summary>
    /// Represents audit trail that can be used to detect changes in data source represented by cluster member.
    /// </summary>
    public interface IAuditTrail
    {
        /// <summary>
        /// Gets sequence number of the last record in transaction log.
        /// </summary>
        long CurrentSequenceNumber { get; }

        /// <summary>
        /// Creates replication message that contains all entries between this checkpoint
        /// and the current state of data source.
        /// </summary>
        /// <param name="baseline">The baseline sequence number of the record in transaction log, inclusive.</param>
        /// <param name="target">The target sequence number of the record in transaction log, inclusive.</param>
        /// <param name="token">The token that can be used to cancel this operation.</param>
        /// <returns>The replication message represents all records between baseline and target state of transaction log; <see langword="null"/> if there is no new records in transaction log.</returns>
        Task<IMessage> CreateReplicaMessageAsync(long baseline, long target, CancellationToken token);
    }
}
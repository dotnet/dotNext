using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Replication
{
    /// <summary>
    /// Represents replication log.
    /// </summary>
    public interface IAuditTrail
    {
        /// <summary>
        /// Gets last record in this audit trail.
        /// </summary>
        LogEntryId LastRecord { get; }

        /// <summary>
        /// Gets record by its index.
        /// </summary>
        /// <param name="recordId">The index of the record.</param>
        /// <returns></returns>
        ILogEntry this[in LogEntryId recordId] { get; }

        bool Contains(in LogEntryId recordId);

        LogEntryId GetPrevious(in LogEntryId recordId);

        LogEntryId? GetNext(in LogEntryId recordId);

        /// <summary>
        /// Commits log entry.
        /// </summary>
        /// <param name="entry">The record to be committed.</param>
        /// <returns><see langword="true"/> if entry is committed successfully; <see langword="false"/> if record is rejected.</returns>
        Task<bool> CommitAsync(ILogEntry entry);

        /// <summary>
        /// Gets identifier of ephemeral Initial log record.
        /// </summary>
        /// <remarks>
        /// There is no other records in this log
        /// located before the initial record.
        /// </remarks>
        ref readonly LogEntryId Initial { get; }
    }
}

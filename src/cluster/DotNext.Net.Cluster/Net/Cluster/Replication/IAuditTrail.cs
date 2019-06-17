using System;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Replication
{
    /// <summary>
    /// Represents replication log.
    /// </summary>
    /// <typeparam name="EntryId">The type representing unique identifier of log entry.</typeparam>
    public interface IAuditTrail<EntryId>
        where EntryId : struct, IEquatable<EntryId>
    {
        /// <summary>
        /// Gets last record in this audit trail.
        /// </summary>
        EntryId LastRecord { get; }

        /// <summary>
        /// Gets record by its index.
        /// </summary>
        /// <param name="recordId">The index of the record.</param>
        /// <returns></returns>
        ILogEntry<EntryId> this[in EntryId recordId] { get; }

        /// <summary>
        /// Determines whether the record with the specified
        /// identifier already in transaction log.
        /// </summary>
        /// <param name="recordId">The identifier of the record to check.</param>
        /// <returns><see langword="true"/> if the record identified by <paramref name="recordId"/> is in transaction log; otherwise, <see langword="false"/>.</returns>
        bool Contains(in EntryId recordId);

        /// <summary>
        /// Gets the record preceding to the specified record.
        /// </summary>
        /// <param name="recordId">The record identifier.</param>
        /// <returns>The identifier of the record preceding to <paramref name="recordId"/>.</returns>
        EntryId GetPrevious(in EntryId recordId);

        /// <summary>
        /// Gets the record following the specified record.
        /// </summary>
        /// <param name="recordId">The record identifier.</param>
        /// <returns>The identifier of the record following <paramref name="recordId"/>.</returns>
        EntryId? GetNext(in EntryId recordId);

        /// <summary>
        /// Commits log entry.
        /// </summary>
        /// <param name="entry">The record to be committed.</param>
        /// <returns><see langword="true"/> if entry is committed successfully; <see langword="false"/> if record is rejected.</returns>
        ValueTask CommitAsync(ILogEntry<EntryId> entry);

        /// <summary>
        /// Gets identifier of ephemeral Initial log record.
        /// </summary>
        /// <remarks>
        /// There is no other records in this log
        /// located before the initial record.
        /// </remarks>
        ref readonly EntryId Initial { get; }
    }
}

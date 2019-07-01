using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Replication
{
    using IMessage = Messaging.IMessage;

    /// <summary>
    /// Represents audit trail responsible for maintaining log entries.
    /// </summary>
    public interface IAuditTrail<LogEntry>
        where LogEntry : class, IMessage
    {
        /// <summary>
        /// Gets index of the committed or last log entry.
        /// </summary>
        /// <remarks>
        /// This method is synchronous because returning value should be cached and updated in memory by implementing class.
        /// </remarks>
        /// <param name="committed"><see langword="true"/> to get the index of highest log entry known to be committed; <see langword="false"/> to get the index of the last log entry.</param>
        /// <returns>The index of the log entry.</returns>
        long GetLastIndex(bool committed);

        /// <summary>
        /// Gets log entries in the specified range.
        /// </summary>
        /// <param name="startIndex">The index of the first requested log entry, inclusively.</param>
        /// <param name="endIndex">The index of the last requested log entry, inclusively; <see langword="null"/> to return all log entries started from <paramref name="startIndex"/> to the last existing log entry.</param>
        /// <returns>The collection of log entries.</returns>
        ValueTask<IReadOnlyList<LogEntry>> GetEntriesAsync(long startIndex, long? endIndex = null);

        /// <summary>
        /// Removes uncommitted log entries from this log.
        /// </summary>
        /// <param name="startIndex">The index of the first entry to be removed.</param>
        /// <param name="endIndex">The index of the last entry to be removed; <see langword="null"/> to delete all uncommitted entries started from <paramref name="startIndex"/>.</param>
        /// <returns></returns>
        ValueTask<long> DeleteAsync(long startIndex, long? endIndex = null);

        /// <summary>
        /// Adds uncommitted log entries into this log.
        /// </summary>
        /// <remarks>
        /// This method should updates cached value provided by method <see cref="GetLastIndex"/> called with argument of value <see langword="false"/>.
        /// </remarks>
        /// <param name="entries">The entries to be added into this log.</param>
        /// <returns>Index of the first and last added entry.</returns>
        ValueTask<(long FirstIndex, long LastIndex)> PrepareAsync(IEnumerable<LogEntry> entries);

        /// <summary>
        /// Commits log entries into the underlying storage and marks these entries as committed.
        /// </summary>
        /// <remarks>
        /// This method should updates cached value provided by method <see cref="GetLastIndex"/> called with argument of value <see langword="true"/>.
        /// </remarks>
        /// <param name="startIndex">The index of the first entry to commit, inclusively.</param>
        /// <param name="endIndex">The index of the last entry to commit, inclusively; if <see langword="null"/> then commits all log entries started from <paramref name="startIndex"/> to the last existing log entry.</param>
        /// <returns>The actual number of committed entries.</returns>
        ValueTask<long> CommitAsync(long startIndex, long? endIndex = null);

        /// <summary>
        /// Gets the first ephemeral log entry that is present in the empty log.
        /// </summary>
        /// <remarks>
        /// The first log entry always represents NOP database command and is already committed.
        /// Index of such entry is always 0.
        /// </remarks>
        ref readonly LogEntry First { get; }
    }
}
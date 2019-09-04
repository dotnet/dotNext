using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Replication
{
    /// <summary>
    /// Represents audit trail responsible for maintaining log entries.
    /// </summary>
    public interface IAuditTrail<LogEntry>
        where LogEntry : class, ILogEntry
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
        /// <remarks>
        /// This method may return less entries than <c>endIndex - startIndex + 1</c>. This may happen if the requested entries are committed entries and squashed into the single entry called snapshot.
        /// In this case the first entry in the collection is a snapshot entry.
        /// </remarks>
        /// <param name="startIndex">The index of the first requested log entry, inclusively.</param>
        /// <param name="endIndex">The index of the last requested log entry, inclusively; <see langword="null"/> to return all log entries started from <paramref name="startIndex"/> to the last existing log entry.</param>
        /// <returns>The collection of log entries.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> or <paramref name="endIndex"/> is negative.</exception>
        /// <exception cref="IndexOutOfRangeException"><paramref name="endIndex"/> is greater than the index if the last added entry.</exception>
        /// <seealso cref="ILogEntry.IsSnapshot"/>
        ValueTask<IReadOnlyList<LogEntry>> GetEntriesAsync(long startIndex, long? endIndex = null);

        /// <summary>
        /// Adds uncommitted log entries into this log.
        /// </summary>
        /// <remarks>
        /// This method should updates cached value provided by method <see cref="GetLastIndex"/> called with argument of value <see langword="false"/>.
        /// </remarks>
        /// <param name="entries">The entries to be added into this log.</param>
        /// <param name="startIndex"><see langword="null"/> to append entries into the end of the log; or index from which all previous log entries should be dropped and replaced with new entries.</param>
        /// <returns>Index of the first added entry.</returns>
        /// <exception cref="ArgumentException"><paramref name="entries"/> is empty.</exception>
        ValueTask<long> AppendAsync(IReadOnlyList<LogEntry> entries, long? startIndex = null);

        /// <summary>
        /// The event that is raised when actual commit happen.
        /// </summary>
        event CommitEventHandler<LogEntry> Committed;

        /// <summary>
        /// Commits log entries into the underlying storage and marks these entries as committed.
        /// </summary>
        /// <remarks>
        /// This method should updates cached value provided by method <see cref="GetLastIndex"/> called with argument of value <see langword="true"/>.
        /// This method may force log compaction and squash all committed entries into single entry called snapshot.
        /// </remarks>
        /// <param name="endIndex">The index of the last entry to commit, inclusively; if <see langword="null"/> then commits all log entries started from the first uncommitted entry to the last existing log entry.</param>
        /// <returns>The actual number of committed entries.</returns>
        ValueTask<long> CommitAsync(long? endIndex = null);

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
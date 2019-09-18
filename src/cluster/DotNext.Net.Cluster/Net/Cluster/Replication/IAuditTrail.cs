using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Replication
{
    /// <summary>
    /// Represents audit trail responsible for maintaining log entries.
    /// </summary>
    public interface IAuditTrail
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
        /// Waits for the commit.
        /// </summary>
        /// <param name="index">The index of the log record to be committed.</param>
        /// <param name="timeout">The timeout used to wait for the commit.</param>
        /// <param name="token">The token that can be used to cancel waiting.</param>
        /// <returns>The task representing waiting operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 1.</exception>
        /// <exception cref="OperationCanceledException">The operation has been cancelled.</exception>
        Task WaitForCommitAsync(long index, TimeSpan timeout, CancellationToken token = default);

        /// <summary>
        /// Commits log entries into the underlying storage and marks these entries as committed.
        /// </summary>
        /// <remarks>
        /// This method should updates cached value provided by method <see cref="IAuditTrail.GetLastIndex"/> called with argument of value <see langword="true"/>.
        /// Additionally, it may force log compaction and squash all committed entries into single entry called snapshot.
        /// </remarks>
        /// <param name="endIndex">The index of the last entry to commit, inclusively; if <see langword="null"/> then commits all log entries started from the first uncommitted entry to the last existing log entry.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The actual number of committed entries.</returns>
        /// <exception cref="OperationCanceledException">The operation has been cancelled.</exception>
        ValueTask<long> CommitAsync(long endIndex, CancellationToken token);

        /// <summary>
        /// Commits log entries into the underlying storage and marks these entries as committed.
        /// </summary>
        /// <remarks>
        /// This method should updates cached value provided by method <see cref="IAuditTrail.GetLastIndex"/> called with argument of value <see langword="true"/>.
        /// Additionally, it may force log compaction and squash all committed entries into single entry called snapshot.
        /// </remarks>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The actual number of committed entries.</returns>
        /// <exception cref="OperationCanceledException">The operation has been cancelled.</exception>
        ValueTask<long> CommitAsync(CancellationToken token);

        /// <summary>
        /// Ensures that all committed entries are applied to the underlying data state machine known as database engine.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the method.</returns>
        /// <exception cref="OperationCanceledException">The operation has been cancelled.</exception>
        Task EnsureConsistencyAsync(CancellationToken token = default);
    }

    /// <summary>
    /// Represents a read-only portion of audit trail.
    /// </summary>
    /// <remarks>
    /// The log entries are valid during lifetime of the segment. If segment is disposed then all entries
    /// obtained from it have undefined state.
    /// </remarks>
    /// <typeparam name="LogEntry">The type of the log entry maintained by the audit trail.</typeparam>
    public interface IAuditTrailSegment<out LogEntry> : IReadOnlyList<LogEntry>, IDisposable
        where LogEntry : class, ILogEntry
    {
        /// <summary>
        /// Returns non-<see langword="null"/> value if the first log entry in this list is a snapshot entry
        /// that has the specific index.
        /// </summary>
        long? SnapshotIndex { get; }
    }

    /// <summary>
    /// Represents audit trail responsible for maintaining log entries.
    /// </summary>
    /// <typeparam name="LogEntry">The type of the log entry maintained by the audit trail.</typeparam>
    public interface IAuditTrail<LogEntry> : IAuditTrail
        where LogEntry : class, ILogEntry
    {
        /// <summary>
        /// Gets log entries in the specified range.
        /// </summary>
        /// <remarks>
        /// This method may return less entries than <c>endIndex - startIndex + 1</c>. It may happen if the requested entries are committed entries and squashed into the single entry called snapshot.
        /// In this case the first entry in the collection is a snapshot entry. Additionally, the caller must call <see cref="IDisposable.Dispose"/> to release resources associated
        /// with the audit trail segment with entries.
        /// </remarks>
        /// <param name="startIndex">The index of the first requested log entry, inclusively.</param>
        /// <param name="endIndex">The index of the last requested log entry, inclusively.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The collection of log entries.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> or <paramref name="endIndex"/> is negative.</exception>
        /// <exception cref="IndexOutOfRangeException"><paramref name="endIndex"/> is greater than the index of the last added entry.</exception>
        /// <seealso cref="ILogEntry.IsSnapshot"/>
        ValueTask<IAuditTrailSegment<LogEntry>> GetEntriesAsync(long startIndex, long endIndex, CancellationToken token);

        /// <summary>
        /// Gets log entries starting from the specified index to the last log entry.
        /// </summary>
        /// <param name="startIndex">The index of the first requested log entry, inclusively.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The collection of log entries.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> is negative.</exception>
        /// <seealso cref="ILogEntry.IsSnapshot"/>
        ValueTask<IAuditTrailSegment<LogEntry>> GetEntriesAsync(long startIndex, CancellationToken token);

        /// <summary>
        /// Adds uncommitted log entries into this log.
        /// </summary>
        /// <remarks>
        /// This method should updates cached value provided by method <see cref="IAuditTrail.GetLastIndex"/> called with argument of value <see langword="false"/>.
        /// </remarks>
        /// <param name="entries">The entries to be added into this log.</param>
        /// <param name="startIndex">The index from which all previous log entries should be dropped and replaced with new entries.</param>
        /// <returns>The task representing asynchronous state of the method.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="startIndex"/> is less than the index of the last committed entry.</exception>
        ValueTask AppendAsync(IReadOnlyList<LogEntry> entries, long startIndex);

        /// <summary>
        /// Adds uncommitted log entries into this log.
        /// </summary>
        /// <remarks>
        /// The supplying function must return <see langword="null"/> if it cannot provide more log entries.
        /// </remarks>
        /// <param name="supplier">Stateful function that is responsible for supplying log entries.</param>
        /// <param name="startIndex">The index from which all previous log entries should be dropped and replaced with new entries.</param>
        /// <returns>The task representing asynchronous state of the method.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="startIndex"/> is less than the index of the last committed entry; or the collection of entries contains the snapshot entry.</exception>
        ValueTask AppendAsync(Func<ValueTask<LogEntry>> supplier, long startIndex);  //TODO: Should be replaced with IAsyncEnumerator in .NET Standard 2.1

        /// <summary>
        /// Adds uncommitted log entries to the end of this log.
        /// </summary>
        /// <remarks>
        /// This method should updates cached value provided by method <see cref="IAuditTrail.GetLastIndex"/> called with argument of value <see langword="false"/>.
        /// </remarks>
        /// <param name="entries">The entries to be added into this log.</param>
        /// <returns>Index of the first added entry.</returns>
        /// <exception cref="ArgumentException"><paramref name="entries"/> is empty.</exception>
        /// <exception cref="InvalidOperationException">The collection of entries contains the snapshot entry.</exception>
        ValueTask<long> AppendAsync(IReadOnlyList<LogEntry> entries);

        /// <summary>
        /// Adds uncommitted log entry to the end of this log.
        /// </summary>
        /// <remarks>
        /// This is the only method that can be used for snapshot installation.
        /// The behavior of the method depends on the <see cref="ILogEntry.IsSnapshot"/> property.
        /// If log entry is a snapshot then the method erases all committed log entries prior to <paramref name="startIndex"/>.
        /// If it is not, the method behaves in the same way as <see cref="AppendAsync(IReadOnlyList{LogEntry}, long)"/>.
        /// </remarks>
        /// <param name="entry">The uncommitted log entry to be added into this audit trail.</param>
        /// <param name="startIndex">The index of the </param>
        /// <returns>The task representing asynchronous state of the method.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="startIndex"/> is less than the index of the last committed entry and <paramref name="entry"/> is not a snapshot.</exception>
        ValueTask AppendAsync(LogEntry entry, long startIndex);

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
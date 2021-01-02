using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO.Log
{
    using Timeout = Threading.Timeout;

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
        /// <returns><see langword="true"/> if log entry with the specified <paramref name="index"/> is committed; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 1.</exception>
        /// <exception cref="OperationCanceledException">The operation has been cancelled.</exception>
        /// <exception cref="TimeoutException">Timeout occurred.</exception>
        async Task<bool> WaitForCommitAsync(long index, TimeSpan timeout, CancellationToken token = default)
        {
            if (index < 0L)
                throw new ArgumentOutOfRangeException(nameof(index));
            for (var timeoutMeasurement = new Timeout(timeout); GetLastIndex(true) < index; await WaitForCommitAsync(timeout, token).ConfigureAwait(false))
            {
                if (!timeoutMeasurement.RemainingTime.TryGetValue(out timeout))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Waits for the commit.
        /// </summary>
        /// <param name="timeout">The timeout used to wait for the commit.</param>
        /// <param name="token">The token that can be used to cancel waiting.</param>
        /// <returns><see langword="true"/> if log entry is committed; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="OperationCanceledException">The operation has been cancelled.</exception>
        Task<bool> WaitForCommitAsync(TimeSpan timeout, CancellationToken token);

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
        ValueTask<long> CommitAsync(long endIndex, CancellationToken token = default);

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
        ValueTask<long> CommitAsync(CancellationToken token = default);

        /// <summary>
        /// Creates backup of this audit trail.
        /// </summary>
        /// <param name="output">The stream used to store backup.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>A task representing state of asynchronous execution.</returns>
        /// <exception cref="NotSupportedException">Backup is not supported by this implementation of audit trail.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        Task CreateBackupAsync(Stream output, CancellationToken token = default)
            => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.FromException(new NotSupportedException());

        /// <summary>
        /// Initializes audit trail.
        /// </summary>
        /// <remarks>
        /// This action may perform cache initialization or other internal data structures.
        /// It can save the performance of the first modification performed to this log.
        /// </remarks>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>A task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        Task InitializeAsync(CancellationToken token = default);

        /// <summary>
        /// Gets log entries in the specified range.
        /// </summary>
        /// <remarks>
        /// This method may return less entries than <c>endIndex - startIndex + 1</c>. It may happen if the requested entries are committed entries and squashed into the single entry called snapshot.
        /// In this case the first entry in the collection is a snapshot entry. Additionally, the caller must call <see cref="IDisposable.Dispose"/> to release resources associated
        /// with the audit trail segment with entries.
        /// </remarks>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="reader">The reader of the log entries.</param>
        /// <param name="startIndex">The index of the first requested log entry, inclusively.</param>
        /// <param name="endIndex">The index of the last requested log entry, inclusively.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The collection of log entries.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> or <paramref name="endIndex"/> is negative.</exception>
        /// <exception cref="IndexOutOfRangeException"><paramref name="endIndex"/> is greater than the index of the last added entry.</exception>
        /// <seealso cref="ILogEntry.IsSnapshot"/>
        ValueTask<TResult> ReadAsync<TResult>(Func<IReadOnlyList<ILogEntry>, long?, CancellationToken, ValueTask<TResult>> reader, long startIndex, long endIndex, CancellationToken token = default);

        /// <summary>
        /// Gets log entries starting from the specified index to the last log entry.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="reader">The reader of the log entries.</param>
        /// <param name="startIndex">The index of the first requested log entry, inclusively.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The collection of log entries.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> is negative.</exception>
        /// <seealso cref="ILogEntry.IsSnapshot"/>
        ValueTask<TResult> ReadAsync<TResult>(Func<IReadOnlyList<ILogEntry>, long?, CancellationToken, ValueTask<TResult>> reader, long startIndex, CancellationToken token = default);

        /// <summary>
        /// Dropes the uncommitted entries starting from the specified position to the end of the log.
        /// </summary>
        /// <param name="startIndex">The index of the first log entry to be dropped.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The actual number of dropped entries.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="startIndex"/> represents index of the committed entry.</exception>
        ValueTask<long> DropAsync(long startIndex, CancellationToken token = default);
    }

    /// <summary>
    /// Represents audit trail responsible for maintaining log entries.
    /// </summary>
    /// <typeparam name="TEntry">The interface type of the log entry maintained by the audit trail.</typeparam>
    public interface IAuditTrail<TEntry> : IAuditTrail
        where TEntry : class, ILogEntry
    {
        /// <summary>
        /// Gets log entries in the specified range.
        /// </summary>
        /// <remarks>
        /// This method may return less entries than <c>endIndex - startIndex + 1</c>. It may happen if the requested entries are committed entries and squashed into the single entry called snapshot.
        /// In this case the first entry in the collection is a snapshot entry. Additionally, the caller must call <see cref="IDisposable.Dispose"/> to release resources associated
        /// with the audit trail segment with entries.
        /// </remarks>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="reader">The reader of the log entries.</param>
        /// <param name="startIndex">The index of the first requested log entry, inclusively.</param>
        /// <param name="endIndex">The index of the last requested log entry, inclusively.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The collection of log entries.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> or <paramref name="endIndex"/> is negative.</exception>
        /// <exception cref="IndexOutOfRangeException"><paramref name="endIndex"/> is greater than the index of the last added entry.</exception>
        /// <seealso cref="ILogEntry.IsSnapshot"/>
        ValueTask<TResult> ReadAsync<TResult>(ILogEntryConsumer<TEntry, TResult> reader, long startIndex, long endIndex, CancellationToken token = default);

        /// <summary>
        /// Gets log entries in the specified range.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="reader">The reader of the log entries.</param>
        /// <param name="startIndex">The index of the first requested log entry, inclusively.</param>
        /// <param name="endIndex">The index of the last requested log entry, inclusively.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The collection of log entries.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> or <paramref name="endIndex"/> is negative.</exception>
        /// <exception cref="IndexOutOfRangeException"><paramref name="endIndex"/> is greater than the index of the last added entry.</exception>
        ValueTask<TResult> ReadAsync<TResult>(Func<IReadOnlyList<TEntry>, long?, CancellationToken, ValueTask<TResult>> reader, long startIndex, long endIndex, CancellationToken token = default)
            => ReadAsync(new LogEntryConsumer<TEntry, TResult>(reader), startIndex, endIndex, token);

        /// <inheritdoc/>
        ValueTask<TResult> IAuditTrail.ReadAsync<TResult>(Func<IReadOnlyList<ILogEntry>, long?, CancellationToken, ValueTask<TResult>> reader, long startIndex, long endIndex, CancellationToken token)
            => ReadAsync<TResult>(new LogEntryConsumer<TEntry, TResult>(reader), startIndex, endIndex, token);

        /// <summary>
        /// Gets log entries starting from the specified index to the last log entry.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="reader">The reader of the log entries.</param>
        /// <param name="startIndex">The index of the first requested log entry, inclusively.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The collection of log entries.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> is negative.</exception>
        /// <seealso cref="ILogEntry.IsSnapshot"/>
        ValueTask<TResult> ReadAsync<TResult>(ILogEntryConsumer<TEntry, TResult> reader, long startIndex, CancellationToken token = default);

        /// <summary>
        /// Gets log entries starting from the specified index to the last log entry.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="reader">The reader of the log entries.</param>
        /// <param name="startIndex">The index of the first requested log entry, inclusively.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The collection of log entries.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> is negative.</exception>
        ValueTask<TResult> ReadAsync<TResult>(Func<IReadOnlyList<TEntry>, long?, CancellationToken, ValueTask<TResult>> reader, long startIndex, CancellationToken token = default)
            => ReadAsync(new LogEntryConsumer<TEntry, TResult>(reader), startIndex, token);

        /// <inheritdoc/>
        ValueTask<TResult> IAuditTrail.ReadAsync<TResult>(Func<IReadOnlyList<ILogEntry>, long?, CancellationToken, ValueTask<TResult>> reader, long startIndex, CancellationToken token)
            => ReadAsync<TResult>(new LogEntryConsumer<TEntry, TResult>(reader), startIndex, token);

        /// <summary>
        /// Adds uncommitted log entries into this log.
        /// </summary>
        /// <remarks>
        /// The supplying function must return <see langword="null"/> if it cannot provide more log entries.
        /// </remarks>
        /// <typeparam name="TEntryImpl">The actual type of the log entry returned by the supplier.</typeparam>
        /// <param name="entries">Stateful object that is responsible for supplying log entries.</param>
        /// <param name="startIndex">The index from which all previous log entries should be dropped and replaced with new entries.</param>
        /// <param name="skipCommitted"><see langword="true"/> to skip committed entries from <paramref name="entries"/> instead of throwing exception.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the method.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="startIndex"/> is less than the index of the last committed entry and <paramref name="skipCommitted"/> is <see langword="false"/>; or the collection of entries contains the snapshot entry.</exception>
        ValueTask AppendAsync<TEntryImpl>(ILogEntryProducer<TEntryImpl> entries, long startIndex, bool skipCommitted = false, CancellationToken token = default)
            where TEntryImpl : notnull, TEntry;

        /// <summary>
        /// Adds uncommitted log entries to the end of this log.
        /// </summary>
        /// <remarks>
        /// This method should updates cached value provided by method <see cref="IAuditTrail.GetLastIndex"/> called with argument of value <see langword="false"/>.
        /// </remarks>
        /// <typeparam name="TEntryImpl">The actual type of the log entry returned by the supplier.</typeparam>
        /// <param name="entries">The entries to be added into this log.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The index of the first added entry.</returns>
        /// <exception cref="ArgumentException"><paramref name="entries"/> is empty.</exception>
        /// <exception cref="InvalidOperationException">The collection of entries contains the snapshot entry.</exception>
        ValueTask<long> AppendAsync<TEntryImpl>(ILogEntryProducer<TEntryImpl> entries, CancellationToken token = default)
            where TEntryImpl : notnull, TEntry;

        /// <summary>
        /// Adds uncommitted log entry to the end of this log.
        /// </summary>
        /// <remarks>
        /// This is the only method that can be used for snapshot installation.
        /// The behavior of the method depends on the <see cref="ILogEntry.IsSnapshot"/> property.
        /// If log entry is a snapshot then the method erases all committed log entries prior to <paramref name="startIndex"/>.
        /// If it is not, the method behaves in the same way as <see cref="AppendAsync{TEntryImpl}(ILogEntryProducer{TEntryImpl}, long, bool, CancellationToken)"/>.
        /// </remarks>
        /// <typeparam name="TEntryImpl">The actual type of the supplied log entry.</typeparam>
        /// <param name="entry">The uncommitted log entry to be added into this audit trail.</param>
        /// <param name="startIndex">The index from which all previous log entries should be dropped and replaced with the new entry.</param>
        /// <returns>The task representing asynchronous state of the method.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="startIndex"/> is less than the index of the last committed entry and <paramref name="entry"/> is not a snapshot.</exception>
        ValueTask AppendAsync<TEntryImpl>(TEntryImpl entry, long startIndex)
            where TEntryImpl : notnull, TEntry;

        /// <summary>
        /// Adds uncommitted log entry to the end of this log.
        /// </summary>
        /// <remarks>
        /// This method cannot be used to append a snapshot.
        /// </remarks>
        /// <param name="entry">The entry to add.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <typeparam name="TEntryImpl">The actual type of the supplied log entry.</typeparam>
        /// <returns>The index of the added entry.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="entry"/> is the snapshot entry.</exception>
        ValueTask<long> AppendAsync<TEntryImpl>(TEntryImpl entry, CancellationToken token = default)
            where TEntryImpl : notnull, TEntry
            => AppendAsync(new SingleEntryProducer<TEntryImpl>(entry), token);
    }
}

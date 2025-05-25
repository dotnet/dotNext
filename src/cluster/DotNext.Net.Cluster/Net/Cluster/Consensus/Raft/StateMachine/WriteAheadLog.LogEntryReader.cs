using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

partial class WriteAheadLog
{
    /// <summary>
    /// Reads the log entries.
    /// </summary>
    /// <param name="startIndex">The index of the first requested log entry.</param>
    /// <param name="endIndex">The index of the last requested log entry.</param>
    /// <param name="token">The token that can be used to cancel the enumeration.</param>
    /// <returns>Lazy collection of log entries.</returns>
    public ValueTask<LogEntryReader> ReadAsync(long startIndex, long endIndex, CancellationToken token = default)
    {
        ValueTask<LogEntryReader> result;
        if (IsDisposingOrDisposed)
            result = new(GetDisposedTask<LogEntryReader>());
        else if (startIndex < 0L)
            result = ValueTask.FromException<LogEntryReader>(new ArgumentOutOfRangeException(nameof(startIndex)));
        else if (endIndex < 0L || endIndex > LastEntryIndex)
            result = ValueTask.FromException<LogEntryReader>(new ArgumentOutOfRangeException(nameof(endIndex)));
        else if (backgroundTaskFailure?.SourceException is { } exception)
            result = ValueTask.FromException<LogEntryReader>(exception);
        else if (startIndex > endIndex)
            result = new(default(LogEntryReader));
        else
            result = ReadCoreAsync(startIndex, endIndex, token);

        return result;
    }

    private async ValueTask<LogEntryReader> ReadCoreAsync(long startIndex, long endIndex, CancellationToken token)
    {
        lockManager.SetCallerInformation("Enumerate Entries");
        await lockManager.AcquireReadLockAsync(token).ConfigureAwait(false);
        return CreateReader(startIndex, endIndex);
    }

    private LogEntryReader CreateReader(long startIndex, long endIndex)
    {
        var reader = new LogEntryList(stateMachine, startIndex, endIndex, dataPages, metadataPages, out _);
        return Unsafe.BitCast<(LogEntryList, LockManager), LogEntryReader>((reader, lockManager));
    }

    /// <summary>
    /// Represents a reader of the log entries;
    /// </summary>
    /// <remarks>
    /// The caller must dispose the reader to inform the WAL that the entries are subjects for garbage collection.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct LogEntryReader : IReadOnlyList<LogEntry>, IDisposable
    {
        private readonly (LogEntryList List, LockManager Manager) state;

        /// <summary>
        /// Gets the log entry by index.
        /// </summary>
        /// <param name="index">The index of the log entry within the collection.</param>
        public LogEntry this[int index] => state.List[index];

        /// <summary>
        /// Gets the number of log entries in the collection.
        /// </summary>
        public int Count => state.List.Count;

        /// <summary>
        /// Gets the enumerator over the log entries.
        /// </summary>
        /// <returns>The enumerator over the log entries.</returns>
        public IEnumerator<LogEntry> GetEnumerator() => state.List.GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Informs WAL that the reader is no longer used.
        /// </summary>
        public void Dispose()
            => state.Manager?.ReleaseReadLock();
    }
}
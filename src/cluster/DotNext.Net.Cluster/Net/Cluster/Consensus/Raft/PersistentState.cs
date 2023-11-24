using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Collections.Specialized;
using IO.Log;
using static IO.DataTransferObject;
using AsyncTrigger = Threading.AsyncTrigger;

/// <summary>
/// Represents general purpose persistent audit trail compatible with Raft algorithm.
/// </summary>
/// <seealso cref="MemoryBasedStateMachine"/>
/// <seealso cref="DiskBasedStateMachine"/>
public abstract partial class PersistentState : Disposable, IPersistentState
{
    private static readonly Counter<long> ReadRateMeter, WriteRateMeter, CommitRateMeter;

    private protected readonly TagList measurementTags;
    private readonly AsyncTrigger commitEvent;
    private protected readonly LockManager syncRoot;
    private readonly long initialSize;
    private protected readonly BufferManager bufferManager;
    private readonly int bufferSize;
    private protected readonly int concurrentReads;
    private protected readonly WriteMode writeMode;
    private readonly bool parallelIO;

    static PersistentState()
    {
        var meter = new Meter("DotNext.IO.WriteAheadLog");
        ReadRateMeter = meter.CreateCounter<long>("entries-read-count", description: "Number of Log Entries Read");
        WriteRateMeter = meter.CreateCounter<long>("entries-write-count", description: "Number of Log Entries Written");
        CommitRateMeter = meter.CreateCounter<long>("entries-commit-count", description: "Number of Log Entries Comiitted");
    }

    private protected PersistentState(DirectoryInfo path, int recordsPerPartition, Options configuration)
    {
        if (recordsPerPartition < 2 || recordsPerPartition > Partition.MaxRecordsPerPartition)
            throw new ArgumentOutOfRangeException(nameof(recordsPerPartition));
        if (!path.Exists)
            path.Create();
        bufferingConsumer = configuration.CreateBufferingConsumer();
        writeMode = configuration.WriteMode;
        backupCompression = configuration.BackupCompression;
        bufferSize = configuration.BufferSize;
        Location = path;
        this.recordsPerPartition = recordsPerPartition;
        initialSize = configuration.InitialPartitionSize;
        commitEvent = new() { MeasurementTags = configuration.MeasurementTags };
        bufferManager = new(configuration);
        concurrentReads = configuration.MaxConcurrentReads;
        sessionManager = concurrentReads < FastSessionIdPool.MaxReadersCount
            ? new FastSessionIdPool()
            : new SlowSessionIdPool(concurrentReads);
        parallelIO = configuration.ParallelIO;

        syncRoot = new(configuration.MaxConcurrentReads)
        {
            MeasurementTags = configuration.MeasurementTags,
        };

        var partitionTable = new SortedSet<Partition>(Comparer<Partition>.Create(ComparePartitions));

        // load all partitions from file system
        foreach (var file in path.EnumerateFiles())
        {
            if (long.TryParse(file.Name, out var partitionNumber))
            {
                var partition = new Partition(file.Directory!, bufferSize, recordsPerPartition, partitionNumber, in bufferManager, concurrentReads, writeMode, initialSize);
                partition.Initialize();
                partitionTable.Add(partition);
            }
        }

        // constructed sorted list of partitions
        foreach (var partition in partitionTable)
        {
            if (LastPartition is null)
            {
                Debug.Assert(FirstPartition is null);
                FirstPartition = partition;
            }
            else
            {
                LastPartition.Append(partition);
            }

            LastPartition = partition;
        }

        partitionTable.Clear();
        state = new(path, bufferManager.BufferAllocator, configuration.IntegrityCheck, writeMode is not WriteMode.NoFlush);
        measurementTags = configuration.MeasurementTags;

        static int ComparePartitions(Partition x, Partition y) => x.PartitionNumber.CompareTo(y.PartitionNumber);
    }

    private protected static Meter MeterRoot => ReadRateMeter.Meter;

    /// <summary>
    /// Gets path to the folder with Write-Ahead Log files.
    /// </summary>
    protected DirectoryInfo Location { get; }

    /// <inheritdoc/>
    bool IAuditTrail.IsLogEntryLengthAlwaysPresented => true;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private partial Partition CreatePartition(long partitionNumber)
        => new(Location, bufferSize, recordsPerPartition, partitionNumber, in bufferManager, concurrentReads, writeMode, initialSize);

    private ValueTask<TResult> UnsafeReadAsync<TResult>(ILogEntryConsumer<IRaftLogEntry, TResult> reader, int sessionId, long startIndex, long endIndex, int length, bool snapshotRequested, CancellationToken token)
    {
        Debug.Assert(LastPartition is not null);
        Debug.Assert(length > 1);

        var entries = new LogEntryList(this, sessionId, startIndex, endIndex, length, reader.LogEntryMetadataOnly);
        long? snapshotIndex;

        switch ((snapshotRequested, reader.LogEntryMetadataOnly))
        {
            case (true, false):
                Debug.Assert(startIndex == SnapshotInfo.Index);

                return UnsafeReadSnapshotAsync(reader, entries, token);
            case (true, true):
                Debug.Assert(startIndex == SnapshotInfo.Index);

                snapshotIndex = startIndex;
                goto default;
            case (false, _):
                snapshotIndex = null;
                goto default;
            default:
                return reader.ReadAsync<LogEntry, LogEntryList>(entries, snapshotIndex, token);
        }
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<TResult> UnsafeReadSnapshotAsync<TResult>(ILogEntryConsumer<IRaftLogEntry, TResult> reader, LogEntryList entries, CancellationToken token)
    {
        try
        {
            entries.Snapshot = await BeginReadSnapshotAsync(entries.SessionId, token).ConfigureAwait(false);
            return await reader.ReadAsync<LogEntry, LogEntryList>(entries, entries.StartIndex, token).ConfigureAwait(false);
        }
        finally
        {
            EndReadSnapshot(entries.SessionId);
        }
    }

    private ValueTask<TResult> UnsafeReadAsync<TResult>(ILogEntryConsumer<IRaftLogEntry, TResult> reader, int sessionId, long startIndex, long endIndex, CancellationToken token)
    {
        Debug.Assert(endIndex >= startIndex);

        if (startIndex > state.LastIndex)
            return ValueTask.FromException<TResult>(new IndexOutOfRangeException(ExceptionMessages.InvalidEntryIndex(startIndex)));

        if (endIndex > state.LastIndex)
            return ValueTask.FromException<TResult>(new IndexOutOfRangeException(ExceptionMessages.InvalidEntryIndex(endIndex)));

        bool snapshotRequested;
        var snapshotIndex = SnapshotInfo.Index;
        if (snapshotRequested = snapshotIndex > 0L && startIndex <= snapshotIndex)
            endIndex = Math.Max(startIndex = snapshotIndex, endIndex); // adjust startIndex automatically to avoid growth of length

        var length = endIndex - startIndex + 1L;
        if (length > int.MaxValue)
            return ValueTask.FromException<TResult>(new InternalBufferOverflowException(ExceptionMessages.RangeTooBig));

        ReadRateMeter.Add(length, measurementTags);

        SingletonList<LogEntry> list;
        if (length > 1L && LastPartition is not null)
        {
            return UnsafeReadAsync(reader, sessionId, startIndex, endIndex, (int)length, snapshotRequested, token);
        }
        else if (startIndex is 0L)
        {
            list = LogEntry.Initial;
        }
        else if (snapshotRequested)
        {
            return UnsafeReadSnapshotAsync(reader, sessionId, token);
        }
        else if (TryGetPartition(PartitionOf(startIndex)) is { } partition)
        {
            list = partition.Read(sessionId, startIndex, reader.LogEntryMetadataOnly);
        }
        else
        {
            return ValueTask.FromException<TResult>(new MissingPartitionException(startIndex));
        }

        return reader.ReadAsync<LogEntry, SingletonList<LogEntry>>(list, snapshotIndex: null, token);
    }

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
    public ValueTask<TResult> ReadAsync<TResult>(ILogEntryConsumer<IRaftLogEntry, TResult> reader, long startIndex, long endIndex, CancellationToken token = default)
    {
        ValueTask<TResult> result;
        if (IsDisposed)
            result = new(GetDisposedTask<TResult>());
        else if (startIndex < 0L)
            result = ValueTask.FromException<TResult>(new ArgumentOutOfRangeException(nameof(startIndex)));
        else if (endIndex < 0L)
            result = ValueTask.FromException<TResult>(new ArgumentOutOfRangeException(nameof(endIndex)));
        else if (startIndex > endIndex)
            result = reader.ReadAsync<LogEntry, LogEntry[]>(Array.Empty<LogEntry>(), null, token);
        else if (bufferingConsumer is null || reader.LogEntryMetadataOnly)
            result = ReadUnbufferedAsync(reader, startIndex, endIndex, token);
        else
            result = ReadBufferedAsync(reader, startIndex, endIndex, token);

        return result;
    }

    // unbuffered read
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<TResult> ReadUnbufferedAsync<TResult>(ILogEntryConsumer<IRaftLogEntry, TResult> reader, long startIndex, long? endIndex, CancellationToken token)
    {
        await syncRoot.AcquireAsync(LockType.WeakReadLock, token).ConfigureAwait(false);
        var session = sessionManager.Take();
        try
        {
            return await UnsafeReadAsync(reader, session, startIndex, endIndex ?? state.LastIndex, token).ConfigureAwait(false);
        }
        finally
        {
            sessionManager.Return(session);
            syncRoot.Release(LockType.WeakReadLock);
        }
    }

    // buffered read
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<TResult> ReadBufferedAsync<TResult>(ILogEntryConsumer<IRaftLogEntry, TResult> reader, long startIndex, long? endIndex, CancellationToken token)
    {
        Debug.Assert(bufferingConsumer is not null);

        // create buffered copy of all entries
        BufferedRaftLogEntryList bufferedEntries;
        long? snapshotIndex;
        await syncRoot.AcquireAsync(LockType.WeakReadLock, token).ConfigureAwait(false);
        var session = sessionManager.Take();
        try
        {
            (bufferedEntries, snapshotIndex) = await UnsafeReadAsync<(BufferedRaftLogEntryList, long?)>(bufferingConsumer, session, startIndex, endIndex ?? state.LastIndex, token).ConfigureAwait(false);
        }
        finally
        {
            sessionManager.Return(session);
            syncRoot.Release(LockType.WeakReadLock);
        }

        // pass buffered entries to the reader
        try
        {
            return await reader.ReadAsync<BufferedRaftLogEntry, BufferedRaftLogEntryList>(bufferedEntries, snapshotIndex, token).ConfigureAwait(false);
        }
        finally
        {
            bufferedEntries.Dispose();

            // return array back to pool
            if (bufferedEntries.Entries.Array is { Length: > 0 } arrayToReturn)
                bufferingConsumer.Add(arrayToReturn);
        }
    }

    /// <summary>
    /// Gets log entries starting from the specified index to the last log entry.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="reader">The reader of the log entries.</param>
    /// <param name="startIndex">The index of the first requested log entry, inclusively.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The collection of log entries.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex"/> is negative.</exception>
    public ValueTask<TResult> ReadAsync<TResult>(ILogEntryConsumer<IRaftLogEntry, TResult> reader, long startIndex, CancellationToken token = default)
    {
        ValueTask<TResult> result;
        if (IsDisposed)
            result = new(GetDisposedTask<TResult>());
        else if (startIndex < 0L)
            result = ValueTask.FromException<TResult>(new ArgumentOutOfRangeException(nameof(startIndex)));
        else if (startIndex > state.LastIndex)
            result = reader.ReadAsync<LogEntry, LogEntry[]>(Array.Empty<LogEntry>(), null, token);
        else if (bufferingConsumer is null || reader.LogEntryMetadataOnly)
            result = ReadUnbufferedAsync(reader, startIndex, null, token);
        else
            result = ReadBufferedAsync(reader, startIndex, null, token);

        return result;
    }

    private protected ValueTask UnsafeAppendAsync<TEntry>(ILogEntryProducer<TEntry> supplier, long startIndex, bool skipCommitted, CancellationToken token)
        where TEntry : notnull, IRaftLogEntry
    {
        Debug.Assert(startIndex <= state.TailIndex);
        Debug.Assert(supplier.RemainingCount > 0L);

        ValueTask result;
        var count = supplier.RemainingCount;

        if (!bufferManager.IsCachingEnabled)
        {
            result = new(AppendUncachedAsync(supplier, startIndex, skipCommitted, token));
        }
        else if (count is 1L)
        {
            result = AppendCachedAsync(supplier, startIndex, writeThrough: true, skipCommitted, token);
        }
        else if (supplier.LogEntryPayloadAvailableImmediately)
        {
            result = AppendCachedAsync(supplier, startIndex, writeThrough: false, skipCommitted, token);
        }
        else
        {
            // bufferize log entries in parallel with disk I/O
            // (without Task.Run because we want to bufferize the first entry using the current thread)
            var bufferingSupplier = new BufferingLogEntryProducer<TEntry>(supplier, bufferManager.BufferAllocator) { Token = token };
            result = new(Task.WhenAll(bufferingSupplier.BufferizeAsync(), AppendUncachedAsync(bufferingSupplier, startIndex, skipCommitted, token)));
        }

        WriteRateMeter.Add(count, measurementTags);
        return result;
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask AppendCachedAsync<TEntry>(ILogEntryProducer<TEntry> supplier, long startIndex, bool writeThrough, bool skipCommitted, CancellationToken token)
        where TEntry : notnull, IRaftLogEntry
    {
        for (Partition? partition = null; await supplier.MoveNextAsync().ConfigureAwait(false); startIndex++)
        {
            var currentEntry = supplier.Current;

            if (currentEntry.IsSnapshot)
                throw new InvalidOperationException(ExceptionMessages.SnapshotDetected);

            if (startIndex > state.CommitIndex)
            {
                GetOrCreatePartition(startIndex, ref partition);

                var cachedEntry = new CachedLogEntry
                {
                    Content = await currentEntry.ToMemoryAsync(bufferManager.BufferAllocator, token).ConfigureAwait(false),
                    Term = currentEntry.Term,
                    CommandId = currentEntry.CommandId,
                    Timestamp = currentEntry.Timestamp,
                    PersistenceMode = writeThrough ? CachedLogEntryPersistenceMode.SkipBuffer : CachedLogEntryPersistenceMode.CopyToBuffer,
                };

                await partition.WriteAsync(cachedEntry, startIndex, token).ConfigureAwait(false);

                // flush if last entry is added to the partition or the last entry is consumed from the iterator
                if (startIndex == partition.LastIndex || supplier.RemainingCount == 0L)
                    await partition.FlushAsync(token).ConfigureAwait(false);
            }
            else if (!skipCommitted)
            {
                throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
            }
        }

        state.LastIndex = startIndex - 1L;
    }

    private async Task AppendUncachedAsync<TEntry>(ILogEntryProducer<TEntry> supplier, long startIndex, bool skipCommitted, CancellationToken token)
        where TEntry : notnull, IRaftLogEntry
    {
        for (Partition? partition = null; await supplier.MoveNextAsync().ConfigureAwait(false); startIndex++)
        {
            var currentEntry = supplier.Current;

            if (currentEntry.IsSnapshot)
                throw new InvalidOperationException(ExceptionMessages.SnapshotDetected);

            if (startIndex > state.CommitIndex)
            {
                GetOrCreatePartition(startIndex, ref partition);
                await partition.WriteAsync(currentEntry, startIndex, token).ConfigureAwait(false);

                // flush if last entry is added to the partition or the last entry is consumed from the iterator
                if (startIndex == partition.LastIndex || supplier.RemainingCount is 0L)
                    await partition.FlushAsync(token).ConfigureAwait(false);
            }
            else if (!skipCommitted)
            {
                throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
            }
        }

        state.LastIndex = startIndex - 1L;
    }

    /// <inheritdoc/>
    ValueTask IAuditTrail<IRaftLogEntry>.AppendAsync<TEntry>(ILogEntryProducer<TEntry> entries, long startIndex, bool skipCommitted, CancellationToken token)
        => IsDisposed ? new(DisposedTask) : entries.RemainingCount is 0L ? ValueTask.CompletedTask : AppendAsync(entries, startIndex, skipCommitted, token);

    private async ValueTask AppendAsync<TEntry>(ILogEntryProducer<TEntry> entries, long startIndex, bool skipCommitted, CancellationToken token)
        where TEntry : notnull, IRaftLogEntry
    {
        // assuming that we want to add log entry to the tail
        LockType lockType;
        await syncRoot.AcquireAsync(lockType = LockType.WriteLock, token).ConfigureAwait(false);

        try
        {
            var tailIndex = state.TailIndex;
            if (startIndex > tailIndex)
                throw new ArgumentOutOfRangeException(nameof(startIndex));

            // wrong assumption, tail of the log can be rewritten so we need exclusive lock
            if (startIndex != tailIndex)
            {
                // write + compaction lock = exclusive lock
                await syncRoot.AcquireAsync(LockType.CompactionLock, token).ConfigureAwait(false);
                lockType = LockType.ExclusiveLock;
            }

            await UnsafeAppendAsync(entries, startIndex, skipCommitted, token).ConfigureAwait(false);

            // flush updated state. Update index here to guarantee safe reads of recently added log entries
            await state.FlushAsync(in NodeState.IndexesRange).ConfigureAwait(false);
        }
        finally
        {
            syncRoot.Release(lockType);
        }
    }

    private protected abstract ValueTask<long> AppendAndCommitAsync<TEntry>(ILogEntryProducer<TEntry> entries, long startIndex, bool skipCommitted, long commitIndex, CancellationToken token)
        where TEntry : notnull, IRaftLogEntry;

    /// <inheritdoc />
    ValueTask<long> IAuditTrail<IRaftLogEntry>.AppendAndCommitAsync<TEntry>(ILogEntryProducer<TEntry> entries, long startIndex, bool skipCommitted, long commitIndex, CancellationToken token)
    {
        return IsDisposed
            ? new(GetDisposedTask<long>())
            : entries.RemainingCount is 0L
            ? CommitAsync(new long?(commitIndex), token)
            : commitIndex < startIndex && parallelIO
            ? AppendAndCommitAsync(entries, startIndex, skipCommitted, commitIndex, token)
            : AppendAndCommitSlowAsync(entries, startIndex, skipCommitted, commitIndex, token);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<long> AppendAndCommitSlowAsync<TEntry>(ILogEntryProducer<TEntry> entries, long startIndex, bool skipCommitted, long commitIndex, CancellationToken token)
        where TEntry : notnull, IRaftLogEntry
    {
        await AppendAsync(entries, startIndex, skipCommitted, token).ConfigureAwait(false);
        return await CommitAsync(new long?(commitIndex), token).ConfigureAwait(false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask UnsafeAppendAsync<TEntry>(TEntry entry, long startIndex, [NotNull] out Partition? partition, CancellationToken token = default)
        where TEntry : notnull, IRaftLogEntry
    {
        partition = LastPartition;
        GetOrCreatePartition(startIndex, ref partition);
        return partition.WriteAsync(entry, startIndex, token);
    }

    private async ValueTask UnsafeAppendAsync<TEntry>(TEntry entry, long startIndex, CancellationToken token)
        where TEntry : notnull, IRaftLogEntry
    {
        Debug.Assert(startIndex <= state.TailIndex);
        Debug.Assert(startIndex > state.CommitIndex);

        await UnsafeAppendAsync(entry, startIndex, out var partition, token).ConfigureAwait(false);
        await partition.FlushAsync(token).ConfigureAwait(false);

        state.LastIndex = startIndex;
        await state.FlushAsync(in NodeState.IndexesRange).ConfigureAwait(false);

        WriteRateMeter.Add(1L, measurementTags);
    }

    /// <summary>
    /// Adds uncommitted log entry to the end of this log.
    /// </summary>
    /// <remarks>
    /// This is the only method that can be used for snapshot installation.
    /// The behavior of the method depends on the <see cref="ILogEntry.IsSnapshot"/> property.
    /// If log entry is a snapshot then the method erases all committed log entries prior to <paramref name="startIndex"/>.
    /// If it is not, the method behaves in the same way as <see cref="IAuditTrail{TEntry}.AppendAsync{TEntryImpl}(ILogEntryProducer{TEntryImpl}, long, bool, CancellationToken)"/>.
    /// </remarks>
    /// <typeparam name="TEntry">The actual type of the supplied log entry.</typeparam>
    /// <param name="entry">The uncommitted log entry to be added into this audit trail.</param>
    /// <param name="startIndex">The index from which all previous log entries should be dropped and replaced with the new entry.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous state of the method.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="startIndex"/> is less than the index of the last committed entry and <paramref name="entry"/> is not a snapshot.</exception>
    public ValueTask AppendAsync<TEntry>(TEntry entry, long startIndex, CancellationToken token = default)
        where TEntry : notnull, IRaftLogEntry
    {
        if (IsDisposed)
            return new(DisposedTask);

        return entry.IsSnapshot ? InstallSnapshotAsync() : AppendRegularEntryAsync();

        async ValueTask AppendRegularEntryAsync()
        {
            Debug.Assert(!entry.IsSnapshot);

            // assuming that we want to add log entry to the tail
            LockType lockType;
            await syncRoot.AcquireAsync(lockType = LockType.WriteLock, token).ConfigureAwait(false);
            try
            {
                if (startIndex <= state.CommitIndex)
                    throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);

                var tailIndex = state.TailIndex;
                if (startIndex > tailIndex)
                    throw new ArgumentOutOfRangeException(nameof(startIndex));

                if (startIndex != tailIndex)
                {
                    // wrong assumption, tail of the log can be rewritten so we need exclusive lock
                    // write + compaction lock = exclusive lock
                    await syncRoot.AcquireAsync(LockType.CompactionLock, token).ConfigureAwait(false);
                    lockType = LockType.ExclusiveLock;
                }

                await UnsafeAppendAsync(entry, startIndex, token).ConfigureAwait(false);
            }
            finally
            {
                syncRoot.Release(lockType);
            }
        }

        async ValueTask InstallSnapshotAsync()
        {
            Debug.Assert(entry.IsSnapshot);

            Partition? removedHead;

            // Snapshot requires exclusive lock. However, snapshot installation is very rare operation
            await syncRoot.AcquireAsync(LockType.ExclusiveLock, token).ConfigureAwait(false);
            try
            {
                if (startIndex <= state.CommitIndex)
                    throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
                await InstallSnapshotAsync<TEntry>(entry, startIndex).ConfigureAwait(false);
                removedHead = DetachPartitions(startIndex);
            }
            finally
            {
                syncRoot.Release(LockType.ExclusiveLock);
            }

            DeletePartitions(removedHead);
        }
    }

    private async ValueTask<long> AppendUncachedAsync<TEntry>(TEntry entry, CancellationToken token)
        where TEntry : notnull, IRaftLogEntry
    {
        long startIndex;
        await syncRoot.AcquireAsync(LockType.WriteLock, token).ConfigureAwait(false);
        try
        {
            startIndex = state.TailIndex;
            await UnsafeAppendAsync(entry, startIndex, out var partition, token).ConfigureAwait(false);
            await partition.FlushAsync(token).ConfigureAwait(false);
            state.LastIndex = startIndex;
            await state.FlushAsync(in NodeState.IndexesRange).ConfigureAwait(false);
        }
        finally
        {
            syncRoot.Release(LockType.WriteLock);
        }

        WriteRateMeter.Add(1L, measurementTags);
        return startIndex;
    }

    private async ValueTask<long> AppendCachedAsync<TEntry>(TEntry entry, CancellationToken token)
        where TEntry : notnull, IRaftLogEntry
        => await AppendCachedAsync(new CachedLogEntry { Content = await entry.ToMemoryAsync(bufferManager.BufferAllocator, token).ConfigureAwait(false), Term = entry.Term, Timestamp = entry.Timestamp, CommandId = entry.CommandId }, token).ConfigureAwait(false);

    private async ValueTask<long> AppendCachedAsync(CachedLogEntry cachedEntry, CancellationToken token)
    {
        Debug.Assert(bufferManager.IsCachingEnabled);

        long startIndex;
        await syncRoot.AcquireAsync(LockType.WriteLock, token).ConfigureAwait(false);
        try
        {
            // append it to the log
            startIndex = state.TailIndex;
            await UnsafeAppendAsync(cachedEntry, startIndex, out _, token).ConfigureAwait(false);
            state.LastIndex = startIndex;
        }
        finally
        {
            syncRoot.Release(LockType.WriteLock);
        }

        WriteRateMeter.Add(1L, measurementTags);
        return startIndex;
    }

    /// <summary>
    /// Adds uncommitted log entry to the end of this log.
    /// </summary>
    /// <remarks>
    /// This method cannot be used to append a snapshot.
    /// </remarks>
    /// <param name="entry">The entry to add.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="TEntry">The actual type of the supplied log entry.</typeparam>
    /// <returns>The index of the added entry.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="entry"/> is the snapshot entry.</exception>
    public ValueTask<long> AppendAsync<TEntry>(TEntry entry, CancellationToken token = default)
        where TEntry : notnull, IRaftLogEntry
        => AppendAsync(entry, true, token);

    /// <summary>
    /// Adds uncommitted log entry to the end of this log.
    /// </summary>
    /// <remarks>
    /// This method cannot be used to append a snapshot.
    /// </remarks>
    /// <param name="entry">The entry to add.</param>
    /// <param name="addToCache">
    /// <see langword="true"/> to copy the entry to in-memory cache to increase commit performance;
    /// <see langword="false"/> to avoid caching.
    /// </param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="TEntry">The actual type of the supplied log entry.</typeparam>
    /// <returns>The index of the added entry.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="entry"/> is the snapshot entry.</exception>
    public ValueTask<long> AppendAsync<TEntry>(TEntry entry, bool addToCache, CancellationToken token = default)
        where TEntry : notnull, IRaftLogEntry
    {
        ValueTask<long> result;
        if (IsDisposed)
        {
            result = new(GetDisposedTask<long>());
        }
        else if (entry.IsSnapshot)
        {
            result = ValueTask.FromException<long>(new InvalidOperationException(ExceptionMessages.SnapshotDetected));
        }
        else if (bufferManager.IsCachingEnabled && addToCache)
        {
            result = entry is IBinaryLogEntry
                ? AppendCachedAsync(new CachedLogEntry { Content = ((IBinaryLogEntry)entry).ToBuffer(bufferManager.BufferAllocator), Term = entry.Term, Timestamp = entry.Timestamp, CommandId = entry.CommandId }, token)
                : AppendCachedAsync(entry, token);
        }
        else
        {
            result = AppendUncachedAsync(entry, token);
        }

        return result;
    }

    /// <summary>
    /// Adds uncommitted log entries to the end of this log.
    /// </summary>
    /// <typeparam name="TEntry">The actual type of the log entry returned by the supplier.</typeparam>
    /// <param name="entries">The entries to be added into this log.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>Index of the first added entry.</returns>
    /// <exception cref="ArgumentException"><paramref name="entries"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The collection of entries contains the snapshot entry.</exception>
    public async ValueTask<long> AppendAsync<TEntry>(ILogEntryProducer<TEntry> entries, CancellationToken token = default)
        where TEntry : notnull, IRaftLogEntry
    {
        ThrowIfDisposed();
        if (entries.RemainingCount == 0L)
            throw new ArgumentException(ExceptionMessages.EntrySetIsEmpty);
        await syncRoot.AcquireAsync(LockType.WriteLock, token).ConfigureAwait(false);
        var startIndex = state.TailIndex;
        try
        {
            await UnsafeAppendAsync(entries, startIndex, false, token).ConfigureAwait(false);

            // flush updated state. Update index here to guarantee safe reads of recently added log entries
            await state.FlushAsync(in NodeState.IndexesRange).ConfigureAwait(false);
        }
        finally
        {
            syncRoot.Release(LockType.WriteLock);
        }

        return startIndex;
    }

    /// <summary>
    /// Drops the uncommitted entries starting from the specified position to the end of the log.
    /// </summary>
    /// <param name="startIndex">The index of the first log entry to be dropped.</param>
    /// <param name="reuseSpace">
    /// <see langword="true"/> to drop entries quickly without cleaning of the disk space occupied by these entries;
    /// <see langword="false"/> to drop entries and reclaim the disk space occupied by these entries.
    /// </param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The actual number of dropped entries.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="startIndex"/> represents index of the committed entry.</exception>
    public async ValueTask<long> DropAsync(long startIndex, bool reuseSpace = false, CancellationToken token = default)
    {
        ThrowIfDisposed();
        var count = 0L;
        if (startIndex > state.LastIndex)
            goto exit;

        await syncRoot.AcquireAsync(LockType.ExclusiveLock, token).ConfigureAwait(false);
        try
        {
            if (startIndex <= state.CommitIndex)
                throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
            count = state.LastIndex - startIndex + 1L;
            state.LastIndex = startIndex - 1L;
            await state.FlushAsync(in NodeState.IndexesRange).ConfigureAwait(false);

            if (reuseSpace)
                InvalidatePartitions(startIndex);
            else
                DropPartitions(startIndex);
        }
        finally
        {
            syncRoot.Release(LockType.ExclusiveLock);
        }

    exit:
        return count;

        void DropPartitions(long upToIndex)
        {
            for (Partition? partition = LastPartition, previous; partition is not null && partition.FirstIndex >= upToIndex; partition = previous)
            {
                previous = partition.Previous;
                DropPartition(partition);
            }

            InvalidatePartitions(upToIndex);
        }

        void DropPartition(Partition partition)
        {
            if (ReferenceEquals(FirstPartition, partition))
                FirstPartition = partition.Next;
            if (ReferenceEquals(LastPartition, partition))
                LastPartition = partition.Previous;
            partition.Detach();
            DeletePartition(partition);
        }
    }

    /// <inheritdoc />
    ValueTask<long> IAuditTrail.DropAsync(long startIndex, CancellationToken token)
        => DropAsync(startIndex, false, token);

    /// <summary>
    /// Waits for the commit.
    /// </summary>
    /// <param name="token">The token that can be used to cancel waiting.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="OperationCanceledException">The operation has been cancelled.</exception>
    public ValueTask WaitForCommitAsync(CancellationToken token = default)
        => commitEvent.WaitAsync(token);

    /// <summary>
    /// Waits for the commit.
    /// </summary>
    /// <param name="index">The index of the log record to be committed.</param>
    /// <param name="token">The token that can be used to cancel waiting.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 1.</exception>
    /// <exception cref="OperationCanceledException">The operation has been cancelled.</exception>
    public ValueTask WaitForCommitAsync(long index, CancellationToken token = default)
        => commitEvent.SpinWaitAsync(new CommitChecker(state, index), token);

    private protected abstract ValueTask<long> CommitAsync(long? endIndex, CancellationToken token);

    /// <summary>
    /// Commits log entries into the underlying storage and marks these entries as committed.
    /// </summary>
    /// <param name="endIndex">The index of the last entry to commit, inclusively; if <see langword="null"/> then commits all log entries started from the first uncommitted entry to the last existing log entry.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The actual number of committed entries.</returns>
    /// <exception cref="OperationCanceledException">The operation has been cancelled.</exception>
    public ValueTask<long> CommitAsync(long endIndex, CancellationToken token = default) => CommitAsync(new long?(endIndex), token);

    /// <summary>
    /// Commits log entries into the underlying storage and marks these entries as committed.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The actual number of committed entries.</returns>
    /// <exception cref="OperationCanceledException">The operation has been cancelled.</exception>
    public ValueTask<long> CommitAsync(CancellationToken token = default) => CommitAsync(null, token);

    /// <summary>
    /// Initializes this state asynchronously.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result of the method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been cancelled.</exception>
    public virtual Task InitializeAsync(CancellationToken token = default)
        => state.VerifyIntegrity() ? Task.CompletedTask : Task.FromException(new InternalStateBrokenException());

    /// <summary>
    /// Removes all log entries from the log.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result of the method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been cancelled.</exception>
    protected virtual async Task ClearAsync(CancellationToken token = default)
    {
        // invalidate state
        await state.ClearAsync(token).ConfigureAwait(false);

        // invalidate partitions
        DeletePartitions(FirstPartition);
        FirstPartition = LastPartition = null;
    }

    private protected void OnCommit(long count)
    {
        Debug.Assert(count > 0L);

        commitEvent.Signal(resumeAll: true);
        CommitRateMeter.Add(count, measurementTags);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected long GetCommitIndexAndCount(in long? endIndex, out long commitIndex)
    {
        commitIndex = endIndex.HasValue ? Math.Min(state.LastIndex, endIndex.GetValueOrDefault()) : state.LastIndex;
        return commitIndex - state.CommitIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected long GetCommitIndexAndCount(ref long commitIndex)
        => (commitIndex = Math.Min(state.LastIndex, commitIndex)) - state.CommitIndex;

    /// <inheritdoc/>
    bool IPersistentState.IsVotedFor(in ClusterMemberId id) => state.IsVotedFor(id);

    /// <summary>
    /// Gets the current term.
    /// </summary>
    public long Term => state.Term;

    /// <inheritdoc/>
    async ValueTask<long> IPersistentState.IncrementTermAsync(ClusterMemberId member)
    {
        long result;
        await syncRoot.AcquireAsync(LockType.WriteLock).ConfigureAwait(false);
        try
        {
            result = state.IncrementTerm(member);
            await state.FlushAsync(in NodeState.TermAndLastVoteRange).ConfigureAwait(false);
        }
        finally
        {
            syncRoot.Release(LockType.WriteLock);
        }

        return result;
    }

    /// <inheritdoc/>
    async ValueTask IPersistentState.UpdateTermAsync(long term, bool resetLastVote)
    {
        await syncRoot.AcquireAsync(LockType.WriteLock).ConfigureAwait(false);
        try
        {
            state.UpdateTerm(term, resetLastVote);
            await state.FlushAsync(in NodeState.TermAndLastVoteFlagRange).ConfigureAwait(false);
        }
        finally
        {
            syncRoot.Release(LockType.WriteLock);
        }
    }

    /// <inheritdoc/>
    async ValueTask IPersistentState.UpdateVotedForAsync(ClusterMemberId id)
    {
        await syncRoot.AcquireAsync(LockType.WriteLock).ConfigureAwait(false);
        try
        {
            state.UpdateVotedFor(id);
            await state.FlushAsync(in NodeState.LastVoteRange).ConfigureAwait(false);
        }
        finally
        {
            syncRoot.Release(LockType.WriteLock);
        }
    }

    /// <summary>
    /// Releases all resources associated with this audit trail.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> if called from <see cref="IDisposable.Dispose()"/>; <see langword="false"/> if called from finalizer.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            for (Partition? current = FirstPartition, next; current is not null; current = next)
            {
                next = current.Next;
                current.Dispose();
            }

            FirstPartition = LastPartition = null;
            state.Dispose();
            commitEvent.Dispose();
            syncRoot.Dispose();
            bufferingConsumer?.Clear();
        }

        base.Dispose(disposing);
    }
}
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using Collections.Specialized;
using IO.Log;
using Replication;
using static IO.DataTransferObject;
using static Threading.AtomicInt64;
using AsyncManualResetEvent = Threading.AsyncManualResetEvent;

/// <summary>
/// Represents general purpose persistent audit trail compatible with Raft algorithm.
/// </summary>
/// <remarks>
/// The layout of the audit trail file system:
/// <list type="table">
/// <item>
/// <term>node.state</term>
/// <description>file containing internal state of Raft node</description>
/// </item>
/// <item>
/// <term>&lt;partition&gt;</term>
/// <description>file containing log partition with log records</description>
/// </item>
/// <item>
/// <term>&lt;partition&gt;.meta</term>
/// <description>file containing metadata associated with the log partition</description>
/// </item>
/// <item>
/// <term>snapshot</term>
/// <description>file containing snapshot</description>
/// </item>
/// </list>
/// The audit trail supports log compaction. However, it doesn't know how to interpret and reduce log records during compaction.
/// To do that, you can override <see cref="CreateSnapshotBuilder(in PersistentState.SnapshotBuilderContext)"/> method and implement state machine logic.
/// </remarks>
public partial class PersistentState : Disposable, IPersistentState
{
    private static readonly Predicate<PersistentState> IsConsistentPredicate;

    static PersistentState()
    {
        IsConsistentPredicate = IsConsistentCore;

        static bool IsConsistentCore(PersistentState state) => state.IsConsistent;
    }

    private readonly DirectoryInfo location;
    private readonly AsyncManualResetEvent commitEvent;
    private readonly LockManager syncRoot;
    private readonly long initialSize;
    private readonly BufferManager bufferManager;
    private readonly int bufferSize, snapshotBufferSize, concurrentReads;
    private readonly bool replayOnInitialize, writeThrough, evictOnCommit;
    private readonly CompactionMode compaction;

    // diagnostic counters
    private readonly Action<double>? readCounter, writeCounter, compactionCounter, commitCounter;

    // writer for this field must have exclusive async lock
    private Snapshot snapshot;

    /// <summary>
    /// Initializes a new persistent audit trail.
    /// </summary>
    /// <param name="path">The path to the folder to be used by audit trail.</param>
    /// <param name="recordsPerPartition">The maximum number of log entries that can be stored in the single file called partition.</param>
    /// <param name="configuration">The configuration of the persistent audit trail.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="recordsPerPartition"/> is less than 2.</exception>
    public PersistentState(DirectoryInfo path, int recordsPerPartition, Options? configuration = null)
    {
        configuration ??= new();
        if (recordsPerPartition < 2L)
            throw new ArgumentOutOfRangeException(nameof(recordsPerPartition));
        if (!path.Exists)
            path.Create();
        bufferingConsumer = configuration.CreateBufferingConsumer();
        writeThrough = configuration.WriteThrough;
        compaction = configuration.CompactionMode;
        backupCompression = configuration.BackupCompression;
        replayOnInitialize = configuration.ReplayOnInitialize;
        bufferSize = configuration.BufferSize;
        snapshotBufferSize = configuration.SnapshotBufferSize;
        location = path;
        this.recordsPerPartition = recordsPerPartition;
        initialSize = configuration.InitialPartitionSize;
        commitEvent = new(false);
        bufferManager = new(configuration);
        concurrentReads = configuration.MaxConcurrentReads;
        sessionManager = concurrentReads < FastSessionIdPool.MaxReadersCount
            ? new FastSessionIdPool()
            : new SlowSessionIdPool(concurrentReads);

        syncRoot = new(configuration);
        evictOnCommit = configuration.CacheEvictionPolicy == LogEntryCacheEvictionPolicy.OnCommit;

        var partitionTable = new SortedSet<Partition>(Comparer<Partition>.Create(ComparePartitions));

        // load all partitions from file system
        foreach (var file in path.EnumerateFiles())
        {
            if (long.TryParse(file.Name, out var partitionNumber))
            {
                var partition = new Partition(file.Directory!, bufferSize, recordsPerPartition, partitionNumber, in bufferManager, concurrentReads, writeThrough, initialSize);
                partitionTable.Add(partition);
            }
        }

        // constructed sorted list of partitions
        foreach (var partition in partitionTable)
        {
            if (tail is null)
            {
                Debug.Assert(head is null);
                head = partition;
            }
            else
            {
                tail.Append(partition);
            }

            tail = partition;
        }

        partitionTable.Clear();
        state = new(path);
        snapshot = new(path, snapshotBufferSize, in bufferManager, concurrentReads, writeThrough, initialSize: initialSize);
        snapshot.Initialize();

        // cluster config

        // counters
        readCounter = ToDelegate(configuration.ReadCounter);
        writeCounter = ToDelegate(configuration.WriteCounter);
        compactionCounter = ToDelegate(configuration.CompactionCounter);
        commitCounter = ToDelegate(configuration.CommitCounter);

        static int ComparePartitions(Partition x, Partition y)
        {
            var xn = x.PartitionNumber;
            return xn.CompareTo(y.PartitionNumber);
        }

        static Action<double>? ToDelegate(IncrementingEventCounter? counter)
            => counter is null ? null : counter.Increment;
    }

    /// <summary>
    /// Initializes a new persistent audit trail.
    /// </summary>
    /// <param name="path">The path to the folder to be used by audit trail.</param>
    /// <param name="recordsPerPartition">The maximum number of log entries that can be stored in the single file called partition.</param>
    /// <param name="configuration">The configuration of the persistent audit trail.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="recordsPerPartition"/> is less than 2.</exception>
    public PersistentState(string path, int recordsPerPartition, Options? configuration = null)
        : this(new DirectoryInfo(path), recordsPerPartition, configuration)
    {
    }

    /// <summary>
    /// Gets a value indicating that log compaction should
    /// be called manually using <see cref="ForceCompactionAsync(long, CancellationToken)"/>
    /// in the background.
    /// </summary>
    public bool IsBackgroundCompaction => compaction == CompactionMode.Background;

    /// <inheritdoc/>
    bool IAuditTrail.IsLogEntryLengthAlwaysPresented => true;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private partial Partition CreatePartition(long partitionNumber)
        => new(location, bufferSize, recordsPerPartition, partitionNumber, in bufferManager, concurrentReads, writeThrough, initialSize);

    private ValueTask<TResult> UnsafeReadAsync<TResult>(LogEntryConsumer<IRaftLogEntry, TResult> reader, int sessionId, long startIndex, long endIndex, CancellationToken token)
    {
        if (startIndex > state.LastIndex)
            return ValueTask.FromException<TResult>(new IndexOutOfRangeException(ExceptionMessages.InvalidEntryIndex(startIndex)));

        if (endIndex > state.LastIndex)
            return ValueTask.FromException<TResult>(new IndexOutOfRangeException(ExceptionMessages.InvalidEntryIndex(endIndex)));

        var length = endIndex - startIndex + 1L;
        if (length > int.MaxValue)
            return ValueTask.FromException<TResult>(new InternalBufferOverflowException(ExceptionMessages.RangeTooBig));

        readCounter?.Invoke(length);
        if (HasPartitions)
            return ReadEntriesAsync(reader, sessionId, startIndex, endIndex, (int)length, token);

        if (!snapshot.IsEmpty)
            return ReadSnapshotAsync(reader, sessionId, token);

        return ReadInitialOrEmptyEntryAsync(in reader, startIndex == 0L, token);

        async ValueTask<TResult> ReadEntriesAsync(LogEntryConsumer<IRaftLogEntry, TResult> reader, int sessionId, long startIndex, long endIndex, int length, CancellationToken token)
        {
            using var list = bufferManager.AllocLogEntryList(length);
            Debug.Assert(list.Length >= length);

            // try to read snapshot out of the loop
            if (!snapshot.IsEmpty && startIndex <= snapshot.Metadata.Index)
            {
                BufferHelpers.GetReference(in list) = snapshot.Read(sessionId);

                // skip squashed log entries
                startIndex = snapshot.Metadata.Index + 1L;
                length = 1;
            }
            else if (startIndex == 0L)
            {
                list.GetPinnableReference() = LogEntry.Initial;
                startIndex = length = 1;
            }
            else
            {
                length = 0;
            }

            return await ReadEntries(in reader, in list, sessionId, startIndex, endIndex, length, token).ConfigureAwait(false);
        }

        ValueTask<TResult> ReadEntries(in LogEntryConsumer<IRaftLogEntry, TResult> reader, in MemoryOwner<LogEntry> list, int sessionId, long startIndex, long endIndex, int listIndex, CancellationToken token)
        {
            ref var first = ref list.GetPinnableReference();

            // enumerate over partitions in search of log entries
            for (Partition? partition = null; startIndex <= endIndex && TryGetPartition(startIndex, ref partition); startIndex++, listIndex++, token.ThrowIfCancellationRequested())
                Unsafe.Add(ref first, listIndex) = partition.Read(sessionId, startIndex, reader.OptimizationHint);

            return reader.ReadAsync<LogEntry, InMemoryList<LogEntry>>(list.Memory.Slice(0, listIndex), first.SnapshotIndex, token);
        }

        ValueTask<TResult> ReadSnapshotAsync(LogEntryConsumer<IRaftLogEntry, TResult> reader, int sessionId, CancellationToken token)
        {
            var entry = snapshot.Read(sessionId);
            return reader.ReadAsync<LogEntry, SingletonList<LogEntry>>(entry, entry.SnapshotIndex, token);
        }

        ValueTask<TResult> ReadInitialOrEmptyEntryAsync(in LogEntryConsumer<IRaftLogEntry, TResult> reader, bool readEphemeralEntry, CancellationToken token)
            => readEphemeralEntry ? reader.ReadAsync<LogEntry, SingletonList<LogEntry>>(LogEntry.Initial, null, token) : reader.ReadAsync<LogEntry, LogEntry[]>(Array.Empty<LogEntry>(), null, token);
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
    public ValueTask<TResult> ReadAsync<TResult>(LogEntryConsumer<IRaftLogEntry, TResult> reader, long startIndex, long endIndex, CancellationToken token = default)
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
        else if (bufferingConsumer is null || reader.OptimizationHint == LogEntryReadOptimizationHint.MetadataOnly)
            result = ReadUnbufferedAsync(reader, startIndex, endIndex, token);
        else
            result = ReadBufferedAsync(reader, startIndex, endIndex, token);

        return result;
    }

    /// <inheritdoc />
    ValueTask<TResult> IAuditTrail<IRaftLogEntry>.ReadAsync<TResult>(ILogEntryConsumer<IRaftLogEntry, TResult> reader, long startIndex, CancellationToken token)
        => ReadAsync(new LogEntryConsumer<IRaftLogEntry, TResult>(reader), startIndex, token);

    /// <inheritdoc />
    ValueTask<TResult> IAuditTrail<IRaftLogEntry>.ReadAsync<TResult>(Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<TResult>> reader, long startIndex, CancellationToken token)
        => ReadAsync(new LogEntryConsumer<IRaftLogEntry, TResult>(reader), startIndex, token);

    /// <inheritdoc />
    ValueTask<TResult> IAuditTrail.ReadAsync<TResult>(Func<IReadOnlyList<ILogEntry>, long?, CancellationToken, ValueTask<TResult>> reader, long startIndex, CancellationToken token)
        => ReadAsync(new LogEntryConsumer<IRaftLogEntry, TResult>(reader), startIndex, token);

    /// <inheritdoc />
    ValueTask<TResult> IAuditTrail<IRaftLogEntry>.ReadAsync<TResult>(ILogEntryConsumer<IRaftLogEntry, TResult> reader, long startIndex, long endIndex, CancellationToken token)
        => ReadAsync(new LogEntryConsumer<IRaftLogEntry, TResult>(reader), startIndex, endIndex, token);

    /// <inheritdoc />
    ValueTask<TResult> IAuditTrail<IRaftLogEntry>.ReadAsync<TResult>(Func<IReadOnlyList<IRaftLogEntry>, long?, CancellationToken, ValueTask<TResult>> reader, long startIndex, long endIndex, CancellationToken token)
        => ReadAsync(new LogEntryConsumer<IRaftLogEntry, TResult>(reader), startIndex, endIndex, token);

    /// <inheritdoc />
    ValueTask<TResult> IAuditTrail.ReadAsync<TResult>(Func<IReadOnlyList<ILogEntry>, long?, CancellationToken, ValueTask<TResult>> reader, long startIndex, long endIndex, CancellationToken token)
        => ReadAsync(new LogEntryConsumer<IRaftLogEntry, TResult>(reader), startIndex, endIndex, token);

    // unbuffered read
    private async ValueTask<TResult> ReadUnbufferedAsync<TResult>(LogEntryConsumer<IRaftLogEntry, TResult> reader, long startIndex, long? endIndex, CancellationToken token)
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
    private async ValueTask<TResult> ReadBufferedAsync<TResult>(LogEntryConsumer<IRaftLogEntry, TResult> reader, long startIndex, long? endIndex, CancellationToken token)
    {
        Debug.Assert(bufferingConsumer is not null);

        // create buffered copy of all entries
        BufferedRaftLogEntryList bufferedEntries;
        long? snapshotIndex;
        await syncRoot.AcquireAsync(LockType.WeakReadLock, token).ConfigureAwait(false);
        var session = sessionManager.Take();
        try
        {
            (bufferedEntries, snapshotIndex) = await UnsafeReadAsync<(BufferedRaftLogEntryList, long?)>(new(bufferingConsumer), session, startIndex, endIndex ?? state.LastIndex, token).ConfigureAwait(false);
        }
        finally
        {
            sessionManager.Return(session);
            syncRoot.Release(LockType.WeakReadLock);
        }

        // pass buffered entries to the reader
        using (bufferedEntries)
        {
            return await reader.ReadAsync<BufferedRaftLogEntry, BufferedRaftLogEntryList>(bufferedEntries, snapshotIndex, token).ConfigureAwait(false);
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
    public ValueTask<TResult> ReadAsync<TResult>(LogEntryConsumer<IRaftLogEntry, TResult> reader, long startIndex, CancellationToken token = default)
    {
        ValueTask<TResult> result;
        if (IsDisposed)
            result = new(GetDisposedTask<TResult>());
        else if (startIndex < 0L)
            result = ValueTask.FromException<TResult>(new ArgumentOutOfRangeException(nameof(startIndex)));
        else if (startIndex > state.LastIndex)
            result = reader.ReadAsync<LogEntry, LogEntry[]>(Array.Empty<LogEntry>(), null, token);
        else if (bufferingConsumer is null || reader.OptimizationHint == LogEntryReadOptimizationHint.MetadataOnly)
            result = ReadUnbufferedAsync(reader, startIndex, null, token);
        else
            result = ReadBufferedAsync(reader, startIndex, null, token);

        return result;
    }

    private async ValueTask<Partition?> UnsafeInstallSnapshotAsync<TSnapshot>(TSnapshot snapshot, long snapshotIndex)
        where TSnapshot : notnull, IRaftLogEntry
    {
        // Save the snapshot into temporary file to avoid corruption caused by network connection
        string tempSnapshotFile, snapshotFile = this.snapshot.FileName;
        using (var tempSnapshot = new Snapshot(location, snapshotBufferSize, in bufferManager, 0, writeThrough, tempSnapshot: true, initialSize: SnapshotMetadata.Size + snapshot.Length.GetValueOrDefault()))
        {
            tempSnapshotFile = tempSnapshot.FileName;
            await tempSnapshot.WriteAsync(snapshot, snapshotIndex).ConfigureAwait(false);
            await tempSnapshot.FlushAsync().ConfigureAwait(false);
        }

        // Close existing snapshot file
        this.snapshot.Dispose();

        /*
         * Swapping snapshot file is unsafe operation because of potential disk I/O failures.
         * However, event if swapping will fail then it can be recovered manually just by renaming 'snapshot.new' file
         * into 'snapshot'. Both versions of snapshot file stay consistent. That's why stream copying is not an option.
         */
        try
        {
            File.Move(tempSnapshotFile, snapshotFile, true);
        }
        catch (Exception e)
        {
            Environment.FailFast(LogMessages.SnapshotInstallationFailed, e);
        }

        Volatile.Write(ref this.snapshot, await CreateSnapshotAsync().ConfigureAwait(false));

        // 5. Apply snapshot to the underlying state machine
        state.CommitIndex = snapshotIndex;
        state.LastIndex = Math.Max(snapshotIndex, state.LastIndex);

        var session = sessionManager.Take();
        try
        {
            await ApplyCoreAsync(this.snapshot.Read(session)).ConfigureAwait(false);
        }
        finally
        {
            sessionManager.Return(session);
        }

        lastTerm.VolatileWrite(snapshot.Term);
        state.LastApplied = snapshotIndex;
        state.Flush();
        await FlushAsync().ConfigureAwait(false);
        commitEvent.Set(true);
        writeCounter?.Invoke(1D);
        return DetachPartitions(snapshotIndex);

        async ValueTask<Snapshot> CreateSnapshotAsync()
        {
            var result = new Snapshot(location, snapshotBufferSize, in bufferManager, concurrentReads, writeThrough);
            await result.InitializeAsync().ConfigureAwait(false);
            return result;
        }
    }

    private async ValueTask UnsafeAppendAsync<TEntry>(ILogEntryProducer<TEntry> supplier, long startIndex, bool skipCommitted, CancellationToken token)
        where TEntry : notnull, IRaftLogEntry
    {
        Debug.Assert(startIndex <= state.TailIndex);

        writeCounter?.Invoke(supplier.RemainingCount);

        for (Partition? partition = null; await supplier.MoveNextAsync().ConfigureAwait(false); startIndex++)
        {
            if (supplier.Current.IsSnapshot)
                throw new InvalidOperationException(ExceptionMessages.SnapshotDetected);

            if (startIndex > state.CommitIndex)
            {
                GetOrCreatePartition(startIndex, ref partition);
                await partition.WriteAsync(supplier.Current, startIndex, token).ConfigureAwait(false);

                // flush if last entry is added to the partition or the last entry is consumed from the iterator
                if (startIndex == partition.LastIndex || supplier.RemainingCount == 0L)
                    await partition.FlushAsync(token).ConfigureAwait(false);
            }
            else if (!skipCommitted)
            {
                throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
            }
        }

        // flush updated state. Update index here to guarantee safe reads of recently added log entries
        state.LastIndex = startIndex - 1L;
        state.Flush();
    }

    /// <inheritdoc/>
    async ValueTask IAuditTrail<IRaftLogEntry>.AppendAsync<TEntry>(ILogEntryProducer<TEntry> entries, long startIndex, bool skipCommitted, CancellationToken token)
    {
        ThrowIfDisposed();
        if (entries.RemainingCount == 0L)
            return;

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
        }
        finally
        {
            syncRoot.Release(lockType);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask UnsafeAppendAsync<TEntry>(TEntry entry, long startIndex, [NotNull] out Partition? partition, CancellationToken token = default)
        where TEntry : notnull, IRaftLogEntry
    {
        partition = tail;
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
        state.Flush();

        writeCounter?.Invoke(1D);
    }

    private async ValueTask<long> UnsafeAppendAsync<TEntry>(TEntry entry, bool flush, CancellationToken token)
        where TEntry : notnull, IRaftLogEntry
    {
        var startIndex = state.TailIndex;
        await UnsafeAppendAsync(entry, startIndex, out var partition, token).ConfigureAwait(false);
        if (flush)
        {
            await partition.FlushAsync(token).ConfigureAwait(false);
            state.LastIndex = startIndex;
            state.Flush();
        }
        else
        {
            state.LastIndex = startIndex;
        }

        writeCounter?.Invoke(1D);
        return startIndex;
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

        return entry.IsSnapshot ? InstallSnapshotAsync(entry, startIndex, token) : AppendRegularEntryAsync(entry, startIndex, token);

        async ValueTask AppendRegularEntryAsync(TEntry entry, long startIndex, CancellationToken token)
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

        async ValueTask InstallSnapshotAsync(TEntry entry, long startIndex, CancellationToken token)
        {
            Debug.Assert(entry.IsSnapshot);

            Partition? removedHead;

            // Snapshot requires exclusive lock. However, snapshot installation is very rare operation
            await syncRoot.AcquireAsync(LockType.ExclusiveLock, token).ConfigureAwait(false);
            try
            {
                if (startIndex <= state.CommitIndex)
                    throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
                removedHead = await UnsafeInstallSnapshotAsync(entry, startIndex).ConfigureAwait(false);
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
        await syncRoot.AcquireAsync(LockType.WriteLock, token).ConfigureAwait(false);
        long startIndex;
        try
        {
            startIndex = await UnsafeAppendAsync(entry, true, token).ConfigureAwait(false);
        }
        finally
        {
            syncRoot.Release(LockType.WriteLock);
        }

        return startIndex;
    }

    private async ValueTask<long> AppendCachedAsync<TEntry>(TEntry entry, CancellationToken token)
        where TEntry : notnull, IRaftLogEntry
    {
        Debug.Assert(bufferManager.IsCachingEnabled);

        // copy log entry to the memory
        var cachedEntry = new CachedLogEntry(await entry.ToMemoryAsync(bufferManager.BufferAllocator).ConfigureAwait(false), entry.Term, entry.Timestamp, entry.CommandId);

        await syncRoot.AcquireAsync(LockType.WriteLock, token).ConfigureAwait(false);
        long startIndex;
        try
        {
            // append it to the log
            startIndex = await UnsafeAppendAsync(cachedEntry, false, token).ConfigureAwait(false);
        }
        finally
        {
            syncRoot.Release(LockType.WriteLock);
        }

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
            result = new(GetDisposedTask<long>());
        else if (entry.IsSnapshot)
            result = ValueTask.FromException<long>(new InvalidOperationException(ExceptionMessages.SnapshotDetected));
        else if (bufferManager.IsCachingEnabled && addToCache)
            result = AppendCachedAsync(entry, token);
        else
            result = AppendUncachedAsync(entry, token);

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
        }
        finally
        {
            syncRoot.Release(LockType.WriteLock);
        }

        return startIndex;
    }

    /// <summary>
    /// Dropes the uncommitted entries starting from the specified position to the end of the log.
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
            state.Flush();

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
            for (Partition? partition = tail, previous; partition is not null && partition.FirstIndex >= upToIndex; partition = previous)
            {
                previous = partition.Previous;
                DropPartition(partition);
            }

            InvalidatePartitions(upToIndex);
        }

        void DropPartition(Partition partition)
        {
            if (ReferenceEquals(head, partition))
                head = partition.Next;
            if (ReferenceEquals(tail, partition))
                tail = partition.Previous;
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
        => commitEvent.WaitForCommitAsync(NodeState.IsCommittedPredicate, state, index, token);

    // this operation doesn't require write lock
    private async ValueTask BuildSnapshotAsync(int sessionId, long upperBoundIndex, SnapshotBuilder builder, CancellationToken token)
    {
        // Calculate the term of the snapshot
        Partition? current = tail;
        builder.Term = this.TryGetPartition(upperBoundIndex, ref current)
            ? current.GetTerm(upperBoundIndex)
            : throw new MissingPartitionException(upperBoundIndex);

        // Initialize builder with snapshot record
        await builder.InitializeAsync(sessionId).ConfigureAwait(false);

        current = head;
        Debug.Assert(current is not null);
        for (long startIndex = snapshot.Metadata.Index + 1L, currentIndex = startIndex; TryGetPartition(builder, startIndex, upperBoundIndex, ref currentIndex, ref current) && current is not null && startIndex <= upperBoundIndex; currentIndex++, token.ThrowIfCancellationRequested())
        {
            await ApplyIfNotEmptyAsync(builder, current.Read(sessionId, currentIndex)).ConfigureAwait(false);
        }

        // update counter
        compactionCounter?.Invoke(upperBoundIndex - snapshot.Metadata.Index);

        bool TryGetPartition(SnapshotBuilder builder, long startIndex, long endIndex, ref long currentIndex, ref Partition? partition)
        {
            builder.AdjustIndex(startIndex, endIndex, ref currentIndex);
            return currentIndex.IsBetween(startIndex, endIndex, BoundType.Closed) && this.TryGetPartition(currentIndex, ref partition);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ValueTask ApplyIfNotEmptyAsync(SnapshotBuilder builder, LogEntry entry)
            => entry.IsEmpty ? ValueTask.CompletedTask : builder.ApplyAsync(entry);
    }

    private async ValueTask<Partition?> UnsafeInstallSnapshotAsync(SnapshotBuilder builder, long snapshotIndex)
    {
        // Persist snapshot (cannot be canceled to avoid inconsistency)
        await builder.BuildAsync(snapshotIndex).ConfigureAwait(false);

        // Remove squashed partitions
        return DetachPartitions(snapshotIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsCompactionRequired(long upperBoundIndex)
        => upperBoundIndex - Volatile.Read(ref snapshot).Metadata.Index >= recordsPerPartition;

    // In case of background compaction we need to have 1 fully committed partition as a divider
    // between partitions produced during writes and partitions to be compacted.
    // This restriction guarantees that compaction and writer thread will not be concurrent
    // when modifying Partition.next and Partition.previous fields need to keep sorted linked list
    // consistent and sorted.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long GetBackgroundCompactionCount(out long snapshotIndex)
    {
        snapshotIndex = Volatile.Read(ref snapshot).Metadata.Index;
        return Math.Max(((state.LastApplied - snapshotIndex) / recordsPerPartition) - 1L, 0L);
    }

    /// <summary>
    /// Gets approximate number of partitions that can be compacted.
    /// </summary>
    public long CompactionCount
        => compaction == CompactionMode.Background ? GetBackgroundCompactionCount(out _) : 0L;

    /// <summary>
    /// Forces log compaction.
    /// </summary>
    /// <remarks>
    /// Full compaction may be time-expensive operation. In this case,
    /// all readers will be blocked until the end of the compaction.
    /// Therefore, <paramref name="count"/> can be used to reduce
    /// lock contention between compaction and readers. If it is <c>1</c>
    /// then compaction range is limited to the log entries contained in the single partition.
    /// This may be helpful if manual compaction is triggered by the background job.
    /// The job can wait for the commit using <see langword="WaitForCommitAsync(CancellationToken)"/>
    /// and then call this method with appropriate number of partitions to be collected
    /// according with <see cref="CompactionCount"/> property.
    /// </remarks>
    /// <param name="count">The number of partitions to be compacted.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of this operation.</returns>
    /// <exception cref="ObjectDisposedException">This log is disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask ForceCompactionAsync(long count, CancellationToken token)
    {
        ValueTask result;
        if (IsDisposed)
        {
            result = new(DisposedTask);
        }
        else if (count < 0L)
        {
            result = ValueTask.FromException(new ArgumentOutOfRangeException(nameof(count)));
        }
        else if (count == 0L || !IsBackgroundCompaction)
        {
            result = new();
        }
        else
        {
            result = ForceBackgroundCompactionAsync(count, token);
        }

        return result;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        long ComputeUpperBoundIndex(long count)
        {
            count = Math.Min(count, GetBackgroundCompactionCount(out var snapshotIndex));
            return checked((recordsPerPartition * count) + snapshotIndex);
        }

        async ValueTask ForceBackgroundCompactionAsync(long count, CancellationToken token)
        {
            Partition? removedHead;
            var builder = CreateSnapshotBuilder();
            if (builder is null)
                return;

            try
            {
                var upperBoundIndex = 0L;

                // initialize builder with log entries (read-only)
                await syncRoot.AcquireAsync(LockType.WeakReadLock, token).ConfigureAwait(false);
                var session = sessionManager.Take();
                try
                {
                    // check compaction range again because snapshot index can be modified by snapshot installation method
                    upperBoundIndex = ComputeUpperBoundIndex(count);
                    if (!IsCompactionRequired(upperBoundIndex))
                        return;

                    // construct snapshot (read-only operation)
                    await BuildSnapshotAsync(session, upperBoundIndex, builder, token).ConfigureAwait(false);
                }
                finally
                {
                    sessionManager.Return(session);
                    syncRoot.Release(LockType.WeakReadLock);
                }

                // rewrite snapshot as well as remove log entries (write access required)
                await syncRoot.AcquireAsync(LockType.CompactionLock, token).ConfigureAwait(false);
                try
                {
                    removedHead = await UnsafeInstallSnapshotAsync(builder, upperBoundIndex).ConfigureAwait(false);
                }
                finally
                {
                    syncRoot.Release(LockType.CompactionLock);
                }
            }
            finally
            {
                builder.Dispose();
            }

            DeletePartitions(removedHead);
        }
    }

    private ValueTask<long> CommitAsync(long? endIndex, CancellationToken token)
    {
        // exclusive lock is required for sequential and foreground compaction;
        // otherwise - write lock which doesn't block background compaction
        return compaction switch
        {
            CompactionMode.Sequential => CommitAndCompactSequentiallyAsync(endIndex, token),
            CompactionMode.Foreground => CommitAndCompactInParallelAsync(endIndex, token),
            _ => CommitWithoutCompactionAsync(endIndex, token),
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        long GetCommitIndexAndCount(in long? endIndex, out long commitIndex)
        {
            var startIndex = state.CommitIndex + 1L;
            commitIndex = endIndex.HasValue ? Math.Min(state.LastIndex, endIndex.GetValueOrDefault()) : state.LastIndex;
            return commitIndex - startIndex + 1L;
        }

        async ValueTask<long> CommitAndCompactSequentiallyAsync(long? endIndex, CancellationToken token)
        {
            Partition? removedHead;
            long count;
            await syncRoot.AcquireAsync(LockType.ExclusiveLock, token).ConfigureAwait(false);
            var session = sessionManager.Take();
            try
            {
                count = GetCommitIndexAndCount(in endIndex, out var commitIndex);
                if (count <= 0L)
                    return 0L;

                state.CommitIndex = commitIndex;
                await ApplyAsync(session, token).ConfigureAwait(false);
                removedHead = await ForceSequentialCompactionAsync(session, commitIndex, token).ConfigureAwait(false);
            }
            finally
            {
                sessionManager.Return(session);
                syncRoot.Release(LockType.ExclusiveLock);
            }

            commitEvent.Set(true);
            commitCounter?.Invoke(count);
            DeletePartitions(removedHead);
            return count;
        }

        async ValueTask<Partition?> ForceSequentialCompactionAsync(int sessionId, long upperBoundIndex, CancellationToken token)
        {
            SnapshotBuilder? builder;
            Partition? removedHead;
            if (IsCompactionRequired(upperBoundIndex) && (builder = CreateSnapshotBuilder()) is not null)
            {
                try
                {
                    await BuildSnapshotAsync(sessionId, upperBoundIndex, builder, token).ConfigureAwait(false);
                    removedHead = await UnsafeInstallSnapshotAsync(builder, upperBoundIndex).ConfigureAwait(false);
                }
                finally
                {
                    builder.Dispose();
                }
            }
            else
            {
                removedHead = null;
            }

            return removedHead;
        }

        async ValueTask<long> CommitWithoutCompactionAsync(long? endIndex, CancellationToken token)
        {
            long count;
            await syncRoot.AcquireAsync(LockType.ExclusiveLock, token).ConfigureAwait(false);
            var session = sessionManager.Take();
            try
            {
                count = GetCommitIndexAndCount(in endIndex, out var commitIndex);
                if (count <= 0L)
                    return 0L;

                state.CommitIndex = commitIndex;
                await ApplyAsync(session, token).ConfigureAwait(false);
            }
            finally
            {
                sessionManager.Return(session);
                syncRoot.Release(LockType.ExclusiveLock);
            }

            commitEvent.Set(true);
            commitCounter?.Invoke(count);
            return count;
        }

        async Task<Partition?> ForceIncrementalCompactionAsync(long upperBoundIndex, CancellationToken token)
        {
            Partition? removedHead;
            SnapshotBuilder? builder;
            if (upperBoundIndex > 0L && (builder = CreateSnapshotBuilder()) is not null)
            {
                var session = sessionManager.Take();
                try
                {
                    await BuildSnapshotAsync(session, upperBoundIndex, builder, token).ConfigureAwait(false);
                    removedHead = await UnsafeInstallSnapshotAsync(builder, upperBoundIndex).ConfigureAwait(false);
                }
                finally
                {
                    sessionManager.Return(session);
                    builder.Dispose();
                }
            }
            else
            {
                removedHead = null;
            }

            return removedHead;
        }

        async ValueTask<long> CommitAndCompactInParallelAsync(long? endIndex, CancellationToken token)
        {
            Partition? removedHead;
            long count;
            await syncRoot.AcquireAsync(LockType.ExclusiveLock, token).ConfigureAwait(false);
            var session = sessionManager.Take();
            try
            {
                count = GetCommitIndexAndCount(in endIndex, out var commitIndex);
                if (count <= 0L)
                    return 0L;

                var compactionIndex = Math.Min(state.CommitIndex, snapshot.Metadata.Index + count);
                state.CommitIndex = commitIndex;
                var compaction = Task.Run(() => ForceIncrementalCompactionAsync(compactionIndex, token));
                try
                {
                    await ApplyAsync(session, token).ConfigureAwait(false);
                }
                finally
                {
                    removedHead = await compaction.ConfigureAwait(false);
                }
            }
            finally
            {
                sessionManager.Return(session);
                syncRoot.Release(LockType.ExclusiveLock);
            }

            commitEvent.Set(true);
            commitCounter?.Invoke(count);
            DeletePartitions(removedHead);
            return count;
        }
    }

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
    /// Applies the command represented by the log entry to the underlying database engine.
    /// </summary>
    /// <param name="entry">The entry to be applied to the state machine.</param>
    /// <remarks>
    /// The base method does nothing so you don't need to call base implementation.
    /// </remarks>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <seealso cref="Commands.CommandInterpreter"/>
    protected virtual ValueTask ApplyAsync(LogEntry entry) => new();

    private ValueTask ApplyCoreAsync(LogEntry entry) => entry.IsEmpty ? new() : ApplyAsync(entry); // skip empty log entry

    /// <summary>
    /// Flushes the underlying data storage.
    /// </summary>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    protected virtual ValueTask FlushAsync() => new();

    private async ValueTask ApplyAsync(int sessionId, long startIndex, CancellationToken token)
    {
        var commitIndex = state.CommitIndex;
        for (Partition? partition = null; startIndex <= commitIndex; state.LastApplied = startIndex++, token.ThrowIfCancellationRequested())
        {
            if (TryGetPartition(startIndex, ref partition))
            {
                var entry = partition.Read(sessionId, startIndex);
                await ApplyCoreAsync(entry).ConfigureAwait(false);
                lastTerm.VolatileWrite(entry.Term);

                // Remove log entry from the cache according to eviction policy
                if (entry.IsBuffered)
                {
                    await partition.PersistCachedEntryAsync(startIndex, entry.Position, evictOnCommit).ConfigureAwait(false);

                    // Flush partition if we are finished or at the last entry in it.
                    if (startIndex == commitIndex || startIndex == partition.LastIndex)
                        await partition.FlushAsync().ConfigureAwait(false);
                }
            }
            else
            {
                throw new MissingPartitionException(startIndex);
            }
        }

        state.Flush();
        await FlushAsync().ConfigureAwait(false);
    }

    private ValueTask ApplyAsync(int sessionId, CancellationToken token)
        => ApplyAsync(sessionId, state.LastApplied + 1L, token);

    /// <summary>
    /// Reconstructs dataset by calling <see cref="ApplyAsync(LogEntry)"/>
    /// for each committed entry.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous state of the method.</returns>
    public async Task ReplayAsync(CancellationToken token = default)
    {
        ThrowIfDisposed();
        await syncRoot.AcquireAsync(LockType.ExclusiveLock, token).ConfigureAwait(false);
        var session = sessionManager.Take();
        try
        {
            LogEntry entry;
            long startIndex;

            // 1. Apply snapshot if it not empty
            if (!snapshot.IsEmpty)
            {
                entry = snapshot.Read(session);
                await ApplyCoreAsync(entry).ConfigureAwait(false);
                lastTerm.VolatileWrite(entry.Term);
                startIndex = snapshot.Metadata.Index;
            }
            else
            {
                startIndex = 0L;
            }

            // 2. Apply all committed entries
            await ApplyAsync(session, startIndex + 1L, token).ConfigureAwait(false);
        }
        finally
        {
            sessionManager.Return(session);
            syncRoot.Release(LockType.ExclusiveLock);
        }
    }

    /// <summary>
    /// Initializes this state asynchronously.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous state of the method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been cancelled.</exception>
    public Task InitializeAsync(CancellationToken token = default)
    {
        if (token.IsCancellationRequested)
            return Task.FromCanceled(token);

        return replayOnInitialize ? ReplayAsync(token) : Task.CompletedTask;
    }

    private bool IsConsistent => state.Term == lastTerm.VolatileRead() && state.CommitIndex == state.LastApplied;

    /// <summary>
    /// Suspens the caller until the log entry with term equal to <see cref="Term"/>
    /// will be committed.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of the asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="TimeoutException">Timeout occurred.</exception>
    public async ValueTask EnsureConsistencyAsync(CancellationToken token)
    {
        ThrowIfDisposed();

        while (!IsConsistent)
            await commitEvent.WaitAsync(IsConsistentPredicate, this, token).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    bool IPersistentState.IsVotedFor(in ClusterMemberId? id) => state.IsVotedFor(id);

    /// <summary>
    /// Gets the current term.
    /// </summary>
    public long Term => state.Term;

    /// <inheritdoc/>
    async ValueTask<long> IPersistentState.IncrementTermAsync()
    {
        long result;
        await syncRoot.AcquireAsync(LockType.WriteLock).ConfigureAwait(false);
        try
        {
            result = state.IncrementTerm();
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
        }
        finally
        {
            syncRoot.Release(LockType.WriteLock);
        }
    }

    /// <inheritdoc/>
    async ValueTask IPersistentState.UpdateVotedForAsync(ClusterMemberId? id)
    {
        await syncRoot.AcquireAsync(LockType.WriteLock).ConfigureAwait(false);
        try
        {
            state.UpdateVotedFor(id);
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
            for (Partition? current = head, next; current is not null; current = next)
            {
                next = current.Next;
                current.Dispose();
            }

            head = tail = null;
            state.Dispose();
            commitEvent.Dispose();
            syncRoot.Dispose();
            snapshot.Dispose();
        }

        base.Dispose(disposing);
    }
}
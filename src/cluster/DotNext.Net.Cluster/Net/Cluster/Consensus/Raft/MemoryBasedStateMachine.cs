using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

using IO.Log;
using Runtime.CompilerServices;

/// <summary>
/// Represents memory-based state machine with snapshotting support.
/// </summary>
/// Memory-based state machine keeps its state in the memory when the program is running.
/// However, the state can be easily recovered by interpreting committed log entries and the snapshot
/// which are persisted on the disk.
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
/// <term>snapshot</term>
/// <description>file containing snapshot</description>
/// </item>
/// </list>
/// The audit trail supports log compaction. However, it doesn't know how to interpret and reduce log records during compaction.
/// To do that, you can override <see cref="CreateSnapshotBuilder(in SnapshotBuilderContext)"/> method and implement state machine logic.
/// </remarks>
public abstract partial class MemoryBasedStateMachine : PersistentState
{
    private static readonly Counter<long> CompactionRateMeter = MeterRoot.CreateCounter<long>("entries-compaction-count", description: "Number of Squashed Log Entries");

    private readonly CompactionMode compaction;
    private readonly bool replayOnInitialize, evictOnCommit;
    private readonly int snapshotBufferSize;

    private long lastTerm;  // term of last committed entry, volatile

    // write to this field must be protected with exclusive async lock
    private Snapshot snapshot;
    private LongLivingSnapshotBuilder? incrementalBuilder; // used by Incremental compaction only

    /// <summary>
    /// Initializes a new memory-based state machine.
    /// </summary>
    /// <param name="path">The path to the folder to be used by audit trail.</param>
    /// <param name="recordsPerPartition">The maximum number of log entries that can be stored in the single file called partition.</param>
    /// <param name="configuration">The configuration of the persistent audit trail.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="recordsPerPartition"/> is less than 2.</exception>
    protected MemoryBasedStateMachine(DirectoryInfo path, int recordsPerPartition, Options? configuration = null)
        : base(path, recordsPerPartition, configuration ??= new())
    {
        compaction = configuration.CompactionMode;
        replayOnInitialize = configuration.ReplayOnInitialize;
        snapshotBufferSize = configuration.SnapshotBufferSize;

        // with concurrent compaction, we will release cached log entries according to partition lifetime
        evictOnCommit = compaction is not CompactionMode.Incremental && configuration.CacheEvictionPolicy is LogEntryCacheEvictionPolicy.OnCommit;
        snapshot = new(path, snapshotBufferSize, in bufferManager, concurrentReads, configuration.WriteMode, initialSize: configuration.InitialPartitionSize);
    }

    /// <summary>
    /// Initializes a new memory-based state machine.
    /// </summary>
    /// <param name="path">The path to the folder to be used by audit trail.</param>
    /// <param name="recordsPerPartition">The maximum number of log entries that can be stored in the single file called partition.</param>
    /// <param name="configuration">The configuration of the persistent audit trail.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="recordsPerPartition"/> is less than 2.</exception>
    protected MemoryBasedStateMachine(string path, int recordsPerPartition, Options? configuration = null)
        : this(new DirectoryInfo(path), recordsPerPartition, configuration)
    {
    }

    private protected sealed override long LastTerm => Volatile.Read(in lastTerm);

    /// <summary>
    /// Gets a value indicating that log compaction should
    /// be called manually using <see cref="ForceCompactionAsync(long, CancellationToken)"/>
    /// in the background.
    /// </summary>
    public bool IsBackgroundCompaction => compaction == CompactionMode.Background;

    // this operation doesn't require write lock
    private async ValueTask BuildSnapshotAsync(int sessionId, long upperBoundIndex, SnapshotBuilder builder, CancellationToken token)
    {
        // Calculate the term of the snapshot
        Partition? current = LastPartition;
        builder.Term = TryGetPartition(upperBoundIndex, ref current)
            ? current.GetTerm(upperBoundIndex)
            : throw new MissingPartitionException(upperBoundIndex);

        // Initialize builder with snapshot record
        await builder.InitializeAsync(sessionId, SnapshotInfo).ConfigureAwait(false);

        current = FirstPartition;
        Debug.Assert(current is not null);
        for (long startIndex = SnapshotInfo.Index + 1L, currentIndex = startIndex; TryGetPartition(builder, startIndex, upperBoundIndex, ref currentIndex, ref current); currentIndex++, token.ThrowIfCancellationRequested())
        {
            await ApplyIfNotEmptyAsync(builder, current.Read(sessionId, currentIndex)).ConfigureAwait(false);
        }

        CompactionRateMeter.Add(upperBoundIndex - SnapshotInfo.Index, measurementTags);
    }

    private bool TryGetPartition(SnapshotBuilder builder, long startIndex, long endIndex, ref long currentIndex, [NotNullWhen(true)] ref Partition? partition)
    {
        builder.AdjustIndex(startIndex, endIndex, ref currentIndex);
        return currentIndex.IsBetween(startIndex, endIndex, BoundType.Closed) && TryGetPartition(currentIndex, ref partition);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ValueTask ApplyIfNotEmptyAsync(SnapshotBuilder builder, LogEntry entry)
        => entry.IsEmpty ? ValueTask.CompletedTask : builder.ApplyAsync(entry);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsCompactionRequired(long upperBoundIndex)
        => upperBoundIndex - SnapshotInfo.Index >= recordsPerPartition;

    // In case of background compaction we need to have 1 fully committed partition as a divider
    // between partitions produced during writes and partitions to be compacted.
    // This restriction guarantees that compaction and writer thread will not be concurrent
    // when modifying Partition.next and Partition.previous fields need to keep sorted linked list
    // consistent and sorted.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long GetBackgroundCompactionCount(out long snapshotIndex)
    {
        snapshotIndex = SnapshotInfo.Index;
        return Math.Max(((LastAppliedEntryIndex - snapshotIndex) / recordsPerPartition) - 1L, 0L);
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
        else if (count is 0L || !IsBackgroundCompaction)
        {
            result = new();
        }
        else
        {
            result = ForceBackgroundCompactionAsync(count, token);
        }

        return result;
    }

    private protected sealed override async ValueTask InstallSnapshotAsync<TSnapshot>(TSnapshot snapshot, long snapshotIndex)
    {
        // Save the snapshot into temporary file to avoid corruption caused by network connection
        string tempSnapshotFile, snapshotFile = this.snapshot.FileName;
        var snapshotLength = snapshot.Length.GetValueOrDefault();
        using (var tempSnapshot = new Snapshot(Location, snapshotBufferSize, in bufferManager, 0, WriteMode.NoFlush, tempSnapshot: true, initialSize: snapshotLength))
        {
            tempSnapshotFile = tempSnapshot.FileName;
            snapshotLength = await tempSnapshot.WriteAsync(snapshot).ConfigureAwait(false);
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
            File.Move(tempSnapshotFile, snapshotFile, overwrite: true);
        }
        catch (Exception e)
        {
            Environment.FailFast(LogMessages.SnapshotInstallationFailed, e);
        }

        this.snapshot = new(Location, snapshotBufferSize, in bufferManager, concurrentReads, writeMode);
        UpdateSnapshotInfo(SnapshotMetadata.Create(snapshot, snapshotIndex, snapshotLength));

        // Apply snapshot to the underlying state machine
        LastCommittedEntryIndex = snapshotIndex;
        LastEntryIndex = Math.Max(snapshotIndex, LastEntryIndex);

        var session = sessionManager.Take();
        try
        {
            await ApplyCoreAsync(new(in SnapshotInfo) { ContentReader = this.snapshot[session] }).ConfigureAwait(false);

            // refresh the current builder
            incrementalBuilder?.Dispose();
            incrementalBuilder = await InitializeLongLivingSnapshotBuilderAsync(session).ConfigureAwait(false);
        }
        finally
        {
            sessionManager.Return(session);
        }

        Volatile.Write(ref lastTerm, snapshot.Term);
        LastAppliedEntryIndex = snapshotIndex;
        await PersistInternalStateAsync(InternalStateScope.IndexesAndSnapshot).ConfigureAwait(false);
        OnCommit(1L);
    }

    private protected sealed override async ValueTask<long> AppendAndCommitAsync<TEntry>(ILogEntryProducer<TEntry> entries, long startIndex, bool skipCommitted, long commitIndex, CancellationToken token)
    {
        /*
         * The following concurrency could happened here:
         * UnsafeAppendAsync invalidates readers of the partition on flush
         * while the readers are in use by ApplyAsync or snapshot building process.
         * It's happening if caching disabled, or EvictOnCommit and Sequential compaction mode.
         * But we can easily ignore this concurrency because invalidation works only when
         * GetSessionReader() is called. In worst case, we will have empty internal buffer
         * of the reader. No additional synchronization is required.
         */
        Debug.Assert(commitIndex < startIndex);

        long count;
        Partition? removedHead;
        ExceptionDispatchInfo? error = null;

        await syncRoot.AcquireAsync(LockType.ExclusiveLock, token).ConfigureAwait(false);
        var session = sessionManager.Take();
        try
        {
            if (startIndex > LastEntryIndex + 1L)
                throw new ArgumentOutOfRangeException(nameof(startIndex));

            // start commit task in parallel
            count = GetCommitIndexAndCount(ref commitIndex);
            LastCommittedEntryIndex = commitIndex;
            var commitTask = count > 0L
                ? compaction switch
                {
                    CompactionMode.Sequential => CommitAndCompactSequentiallyAsync(session, commitIndex, token),
                    CompactionMode.Foreground => CommitAndCompactInParallelAsync(session, count, token),
                    CompactionMode.Incremental => CommitAndCompactIncrementallyAsync(session, token),
                    _ => CommitWithoutCompactionAsync(session, token),
                }
                : Task.FromResult<Partition?>(null);

            // append log entries on this thread
            InternalStateScope scope;
            try
            {
                await UnsafeAppendAsync(entries, startIndex, skipCommitted, token).ConfigureAwait(false);
                scope = InternalStateScope.IndexesAndSnapshot;
            }
            catch (Exception e)
            {
                // cannot append entries
                error = ExceptionDispatchInfo.Capture(e);
                scope = InternalStateScope.Snapshot;
            }

            removedHead = await commitTask.ConfigureAwait(false);
            await PersistInternalStateAsync(scope).ConfigureAwait(false);
        }
        finally
        {
            sessionManager.Return(session);
            syncRoot.Release(LockType.ExclusiveLock);
        }

        if (count > 0L)
            OnCommit(count);

        DeletePartitions(removedHead);
        error?.Throw();
        return count;
    }

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder<>))]
    private async Task<Partition?> CommitAndCompactSequentiallyAsync(int session, long commitIndex, CancellationToken token)
    {
        Partition? removedHead;
        await ApplyAsync(session, token).ConfigureAwait(false);
        if (IsCompactionRequired(commitIndex))
        {
            await ForceSequentialCompactionAsync(session, commitIndex, token).ConfigureAwait(false);
            removedHead = DetachPartitions(commitIndex);
        }
        else
        {
            removedHead = null;
        }

        return removedHead;
    }

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder<>))]
    private async Task<Partition?> CommitWithoutCompactionAsync(int session, CancellationToken token)
    {
        await ApplyAsync(session, token).ConfigureAwait(false);
        return null;
    }

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder<>))]
    private async Task<Partition?> CommitAndCompactInParallelAsync(int session, long count, CancellationToken token)
    {
        var compactionIndex = Math.Min(LastAppliedEntryIndex, SnapshotInfo.Index + count);

        var compactionTask = compactionIndex > 0L
            ? ForceParallelCompactionAsync(compactionIndex, token)
            : Task.CompletedTask;

        try
        {
            await ApplyAsync(session, token).ConfigureAwait(false);
        }
        finally
        {
            await compactionTask.ConfigureAwait(false);
        }

        return DetachPartitions(compactionIndex);
    }

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder<>))]
    private async Task<Partition?> CommitAndCompactIncrementallyAsync(int session, CancellationToken token)
    {
        Partition? removedHead;
        var compactionIndex = LastAppliedEntryIndex;
        var compactionTask = compactionIndex > 0L
            ? ForceIncrementalCompactionAsync(compactionIndex, token)
            : Task.FromResult(false);

        try
        {
            await ApplyAsync(session, token).ConfigureAwait(false);
        }
        finally
        {
            removedHead = await compactionTask.ConfigureAwait(false)
                ? DetachPartitions(compactionIndex)
                : null;
        }

        return removedHead;
    }

    private protected sealed override ValueTask<long> CommitAsync(long? endIndex, CancellationToken token)
    {
        // exclusive lock is required for sequential and foreground compaction;
        // otherwise - write lock which doesn't block background compaction
        return compaction switch
        {
            CompactionMode.Sequential => CommitAndCompactSequentiallyAsync(endIndex, token),
            CompactionMode.Foreground => CommitAndCompactInParallelAsync(endIndex, token),
            CompactionMode.Incremental => CommitAndCompactIncrementallyAsync(endIndex, token),
            _ => CommitWithoutCompactionAsync(endIndex, token),
        };
    }

    private async ValueTask<long> CommitAndCompactSequentiallyAsync(long? endIndex, CancellationToken token)
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

            LastCommittedEntryIndex = commitIndex;
            await ApplyAsync(session, token).ConfigureAwait(false);
            InternalStateScope scope;
            if (IsCompactionRequired(commitIndex))
            {
                await ForceSequentialCompactionAsync(session, commitIndex, token).ConfigureAwait(false);
                removedHead = DetachPartitions(commitIndex);
                scope = InternalStateScope.IndexesAndSnapshot;
            }
            else
            {
                removedHead = null;
                scope = InternalStateScope.Indexes;
            }

            await PersistInternalStateAsync(scope).ConfigureAwait(false);
        }
        finally
        {
            sessionManager.Return(session);
            syncRoot.Release(LockType.ExclusiveLock);
        }

        OnCommit(count);
        DeletePartitions(removedHead);
        return count;
    }

    private async ValueTask<long> CommitWithoutCompactionAsync(long? endIndex, CancellationToken token)
    {
        long count;
        await syncRoot.AcquireAsync(LockType.ExclusiveLock, token).ConfigureAwait(false);
        var session = sessionManager.Take();
        try
        {
            count = GetCommitIndexAndCount(in endIndex, out var commitIndex);
            if (count <= 0L)
                return 0L;

            LastCommittedEntryIndex = commitIndex;
            await ApplyAsync(session, token).ConfigureAwait(false);
            await PersistInternalStateAsync(InternalStateScope.Indexes).ConfigureAwait(false);
        }
        finally
        {
            sessionManager.Return(session);
            syncRoot.Release(LockType.ExclusiveLock);
        }

        OnCommit(count);
        return count;
    }

    private async ValueTask<long> CommitAndCompactInParallelAsync(long? endIndex, CancellationToken token)
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

            var compactionIndex = Math.Min(LastCommittedEntryIndex, SnapshotInfo.Index + count);
            LastCommittedEntryIndex = commitIndex;

            var compactionTask = compactionIndex > 0L
                ? ForceParallelCompactionAsync(compactionIndex, token)
                : Task.CompletedTask;

            try
            {
                await ApplyAsync(session, token).ConfigureAwait(false);
            }
            finally
            {
                await compactionTask.ConfigureAwait(false);
                removedHead = DetachPartitions(compactionIndex);
            }

            await PersistInternalStateAsync(InternalStateScope.IndexesAndSnapshot).ConfigureAwait(false);
        }
        finally
        {
            sessionManager.Return(session);
            syncRoot.Release(LockType.ExclusiveLock);
        }

        OnCommit(count);
        DeletePartitions(removedHead);
        return count;
    }

    private async ValueTask<long> CommitAndCompactIncrementallyAsync(long? endIndex, CancellationToken token)
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

            var compactionIndex = LastAppliedEntryIndex;
            LastCommittedEntryIndex = commitIndex;
            var compactionTask = compactionIndex > 0L
                ? ForceIncrementalCompactionAsync(compactionIndex, token)
                : Task.FromResult(false);
            InternalStateScope scope;

            try
            {
                await ApplyAsync(session, token).ConfigureAwait(false);
            }
            finally
            {
                if (await compactionTask.ConfigureAwait(false))
                {
                    removedHead = DetachPartitions(compactionIndex);
                    scope = InternalStateScope.IndexesAndSnapshot;
                }
                else
                {
                    removedHead = null;
                    scope = InternalStateScope.Indexes;
                }
            }

            await PersistInternalStateAsync(scope).ConfigureAwait(false);
        }
        finally
        {
            sessionManager.Return(session);
            syncRoot.Release(LockType.ExclusiveLock);
        }

        OnCommit(count);
        DeletePartitions(removedHead);
        return count;
    }

    private async ValueTask ForceSequentialCompactionAsync(int sessionId, long upperBoundIndex, CancellationToken token)
    {
        using var builder = CreateSnapshotBuilder();
        await BuildSnapshotAsync(sessionId, upperBoundIndex, builder, token).ConfigureAwait(false);

        // Persist snapshot (cannot be canceled to avoid inconsistency)
        UpdateSnapshotInfo(await builder.BuildAsync(upperBoundIndex).ConfigureAwait(false));
    }

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task ForceParallelCompactionAsync(long upperBoundIndex, CancellationToken token)
    {
        var builder = CreateSnapshotBuilder();
        var session = sessionManager.Take();
        try
        {
            await BuildSnapshotAsync(session, upperBoundIndex, builder, token).ConfigureAwait(false);

            // Persist snapshot (cannot be canceled to avoid inconsistency)
            UpdateSnapshotInfo(await builder.BuildAsync(upperBoundIndex).ConfigureAwait(false));
        }
        finally
        {
            sessionManager.Return(session);
            builder.Dispose();
        }
    }

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder<>))]
    private async Task<bool> ForceIncrementalCompactionAsync(long upperBoundIndex, CancellationToken token)
    {
        var session = sessionManager.Take();
        try
        {
            // initialize snapshot builder if needed
            incrementalBuilder ??= await InitializeLongLivingSnapshotBuilderAsync(session).ConfigureAwait(false);

            long startIndex = incrementalBuilder.LastAppliedIndex + 1L, currentIndex = startIndex;
            for (Partition? partition = FirstPartition; TryGetPartition(incrementalBuilder.Builder, startIndex, upperBoundIndex, ref currentIndex, ref partition); currentIndex++, token.ThrowIfCancellationRequested())
            {
                var entry = partition.Read(session, currentIndex);
                await ApplyIfNotEmptyAsync(incrementalBuilder.Builder, entry).ConfigureAwait(false);
                incrementalBuilder.Builder.Term = entry.Term;
                incrementalBuilder.LastAppliedIndex = currentIndex;
            }
        }
        finally
        {
            sessionManager.Return(session);
        }

        if (IsCompactionRequired(upperBoundIndex))
        {
            incrementalBuilder.Builder.RefreshTimestamp();
            UpdateSnapshotInfo(await incrementalBuilder.Builder.BuildAsync(upperBoundIndex).ConfigureAwait(false));

            CompactionRateMeter.Add(upperBoundIndex - SnapshotInfo.Index, measurementTags);
            return true;
        }

        return false;
    }

    private async ValueTask ForceBackgroundCompactionAsync(long count, CancellationToken token)
    {
        Partition? removedHead;

        using (var builder = CreateSnapshotBuilder())
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
                // Persist snapshot (cannot be canceled to avoid inconsistency)
                UpdateSnapshotInfo(await builder.BuildAsync(upperBoundIndex).ConfigureAwait(false));
                await PersistInternalStateAsync(InternalStateScope.Snapshot).ConfigureAwait(false);

                // Remove squashed partitions
                removedHead = DetachPartitions(upperBoundIndex);
            }
            finally
            {
                syncRoot.Release(LockType.CompactionLock);
            }
        }

        DeletePartitions(removedHead);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        long ComputeUpperBoundIndex(long count)
        {
            count = Math.Min(count, GetBackgroundCompactionCount(out var snapshotIndex));
            return checked((recordsPerPartition * count) + snapshotIndex);
        }
    }

    /// <summary>
    /// Applies the command represented by the log entry to the underlying database engine.
    /// </summary>
    /// <param name="entry">The entry to be applied to the state machine.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <seealso cref="Commands.CommandInterpreter"/>
    protected abstract ValueTask ApplyAsync(LogEntry entry);

    private ValueTask ApplyCoreAsync(LogEntry entry) => entry.IsEmpty ? new() : ApplyAsync(entry); // skip empty log entry

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask ApplyAsync(int sessionId, long startIndex, CancellationToken token)
    {
        var commitIndex = LastCommittedEntryIndex;
        for (Partition? partition = null; startIndex <= commitIndex; LastAppliedEntryIndex = startIndex++, token.ThrowIfCancellationRequested())
        {
            if (TryGetPartition(startIndex, ref partition))
            {
                var entry = partition.Read(sessionId, startIndex, out var persisted);
                await ApplyCoreAsync(entry).ConfigureAwait(false);
                Volatile.Write(ref lastTerm, entry.Term);

                // Remove log entry from the cache according to eviction policy
                if (!persisted)
                {
                    await partition.PersistCachedEntryAsync(startIndex, entry.Position, evictOnCommit).ConfigureAwait(false);

                    // Flush partition if we are finished or at the last entry in it
                    if (startIndex == commitIndex || startIndex == partition.LastIndex)
                        await partition.FlushAsync(token).ConfigureAwait(false);
                }
            }
            else
            {
                throw new MissingPartitionException(startIndex);
            }
        }
    }

    private ValueTask ApplyAsync(int sessionId, CancellationToken token)
        => ApplyAsync(sessionId, LastAppliedEntryIndex + 1L, token);

    /// <summary>
    /// Reconstructs dataset by calling <see cref="ApplyAsync(LogEntry)"/>
    /// for each committed entry.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous state of the method.</returns>
    public async Task ReplayAsync(CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        await syncRoot.AcquireAsync(LockType.ExclusiveLock, token).ConfigureAwait(false);
        var session = sessionManager.Take();
        try
        {
            LogEntry entry;
            long startIndex;

            // 1. Apply snapshot if it not empty
            if (SnapshotInfo.Index > 0L)
            {
                entry = new(in SnapshotInfo) { ContentReader = snapshot[session] };
                await ApplyCoreAsync(entry).ConfigureAwait(false);
                Volatile.Write(ref lastTerm, entry.Term);
                startIndex = entry.Index;
            }
            else
            {
                startIndex = 0L;
            }

            // 2. Apply all committed entries
            await ApplyAsync(session, startIndex += 1L, token).ConfigureAwait(false);

            // 3. Initialize long-living snapshot builder
            if (compaction is CompactionMode.Incremental)
            {
                incrementalBuilder = await InitializeLongLivingSnapshotBuilderAsync(session).ConfigureAwait(false);
                for (Partition? partition = FirstPartition; TryGetPartition(startIndex, ref partition) && partition is not null && startIndex <= LastCommittedEntryIndex; startIndex++)
                {
                    entry = partition.Read(session, startIndex);
                    incrementalBuilder.Builder.Term = entry.Term;
                    await ApplyIfNotEmptyAsync(incrementalBuilder.Builder, entry).ConfigureAwait(false);
                    incrementalBuilder.LastAppliedIndex = startIndex;
                }
            }
        }
        finally
        {
            sessionManager.Return(session);
            syncRoot.Release(LockType.ExclusiveLock);
        }
    }

    /// <inheritdoc />
    public override async Task InitializeAsync(CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        await base.InitializeAsync(token).ConfigureAwait(false);

        if (replayOnInitialize)
            await ReplayAsync(token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected sealed override async Task ClearAsync(CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        await syncRoot.AcquireAsync(LockType.ExclusiveLock, token).ConfigureAwait(false);
        try
        {
            await base.ClearAsync(token).ConfigureAwait(false);

            // delete snapshot
            snapshot.Dispose();
            File.Delete(snapshot.FileName);
            snapshot = new(Location, snapshotBufferSize, in bufferManager, concurrentReads, writeMode);
        }
        finally
        {
            syncRoot.Release(LockType.ExclusiveLock);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            snapshot.Dispose();
            incrementalBuilder?.Dispose();
        }

        base.Dispose(disposing);
    }
}
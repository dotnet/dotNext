namespace DotNext.Net.Cluster.Consensus.Raft;

using static Threading.AtomicInt64;

/// <summary>
/// Represents disk-based state machine.
/// </summary>
/// <remarks>
/// In contrast to <see cref="MemoryBasedStateMachine"/>, disk-based state machine keeps recent changes
/// in the memory. The entire state is fully persisted on the disk. The persisted state can be used as a snapshot.
/// The recent changes can be reconstructed from the committed log entries.
/// </remarks>
public abstract partial class DiskBasedStateMachine : PersistentState
{
    private long lastTerm;  // term of last committed entry, volatile

    /// <summary>
    /// Initializes a new memory-based state machine.
    /// </summary>
    /// <param name="path">The path to the folder to be used by audit trail.</param>
    /// <param name="recordsPerPartition">The maximum number of log entries that can be stored in the single file called partition.</param>
    /// <param name="configuration">The configuration of the persistent audit trail.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="recordsPerPartition"/> is less than 2.</exception>
    protected DiskBasedStateMachine(DirectoryInfo path, int recordsPerPartition, Options? configuration = null)
        : base(path, recordsPerPartition, configuration ??= new())
    {
    }

    /// <summary>
    /// Initializes a new memory-based state machine.
    /// </summary>
    /// <param name="path">The path to the folder to be used by audit trail.</param>
    /// <param name="recordsPerPartition">The maximum number of log entries that can be stored in the single file called partition.</param>
    /// <param name="configuration">The configuration of the persistent audit trail.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="recordsPerPartition"/> is less than 2.</exception>
    protected DiskBasedStateMachine(string path, int recordsPerPartition, Options? configuration = null)
        : this(new DirectoryInfo(path), recordsPerPartition, configuration)
    {
    }

    /// <summary>
    /// Applies the committed log entry to the state machine.
    /// </summary>
    /// <param name="entry">The entry to be applied to the state machine.</param>
    /// <returns>The size of the snapshot, in bytes; <see langword="null"/> to keep the committed log entry in the log.</returns>
    /// <seealso cref="Commands.CommandInterpreter"/>
    protected abstract ValueTask<long?> ApplyAsync(LogEntry entry);

    private ValueTask<long?> ApplyCoreAsync(LogEntry entry) => entry.IsEmpty ? new(default(long?)) : ApplyAsync(entry);

    private protected sealed override long LastTerm => lastTerm.VolatileRead();

    private async ValueTask<long?> ApplyAsync(int sessionId, long startIndex, CancellationToken token)
    {
        var commitIndex = LastCommittedEntryIndex;
        long? removalIndex = null;

        for (Partition? partition = null; startIndex <= commitIndex; LastAppliedEntryIndex = startIndex++, token.ThrowIfCancellationRequested())
        {
            if (TryGetPartition(startIndex, ref partition))
            {
                var entry = partition.Read(sessionId, startIndex, out var persisted);
                var snapshotLength = await ApplyCoreAsync(entry).ConfigureAwait(false);
                lastTerm.VolatileWrite(entry.Term);

                // Remove log entry from the cache according to eviction policy
                if (!persisted)
                {
                    await partition.PersistCachedEntryAsync(startIndex, entry.Position, snapshotLength.HasValue).ConfigureAwait(false);

                    // Flush partition if we are finished or at the last entry in it
                    if (startIndex == commitIndex || startIndex == partition.LastIndex)
                        await partition.FlushAsync().ConfigureAwait(false);
                }

                if (snapshotLength.HasValue)
                {
                    UpdateSnapshotInfo(new SnapshotMetadata(startIndex, DateTimeOffset.UtcNow, entry.Term, snapshotLength.GetValueOrDefault()));
                    removalIndex = startIndex;
                }
            }
            else
            {
                throw new MissingPartitionException(startIndex);
            }
        }

        await PersistInternalStateAsync(includeSnapshotMetadata: removalIndex.HasValue).ConfigureAwait(false);
        return removalIndex;
    }

    private ValueTask<long?> ApplyAsync(int sessionId, CancellationToken token)
        => ApplyAsync(sessionId, LastAppliedEntryIndex + 1L, token);

    private protected sealed override async ValueTask<long> CommitAsync(long? endIndex, CancellationToken token)
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
            var removalIndex = await ApplyAsync(session, token).ConfigureAwait(false);
            removedHead = removalIndex.HasValue ? DetachPartitions(removalIndex.GetValueOrDefault()) : null;
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

    /// <summary>
    /// Initializes internal state of the state machine and replays
    /// committed log entries that were not moved to the snapshot.
    /// </summary>
    /// <remarks>
    /// The method calls <see cref="ApplyAsync(LogEntry)"/> to replay the committed log entries
    /// in the head of the log.
    /// </remarks>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result of the method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been cancelled.</exception>
    public override async Task InitializeAsync(CancellationToken token = default)
    {
        ThrowIfDisposed();
        await syncRoot.AcquireAsync(LockType.ExclusiveLock, token).ConfigureAwait(false);
        var session = sessionManager.Take();
        try
        {
            await ApplyAsync(session, SnapshotInfo.Index + 1L, token).ConfigureAwait(false);
        }
        finally
        {
            sessionManager.Return(session);
            syncRoot.Release(LockType.ExclusiveLock);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Buffers;
    using Collections.Specialized;
    using IO.Log;
    using Replication;
    using static Threading.AtomicInt64;
    using AsyncManualResetEvent = Threading.AsyncManualResetEvent;
    using Timeout = Threading.Timeout;

    /// <summary>
    /// Represents general purpose persistent audit trail compatible with Raft algorithm.
    /// </summary>
    /// <remarks>
    /// The layout of of the audit trail file system:
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
    /// To do that, you can override <see cref="CreateSnapshotBuilder"/> method and implement state machine logic.
    /// </remarks>
    public partial class PersistentState : Disposable, IPersistentState, IAsyncDisposable
    {
        private static readonly Predicate<PersistentState> IsConsistentPredicate;

        static PersistentState()
        {
            IsConsistentPredicate = DelegateHelpers.CreateOpenDelegate<Predicate<PersistentState>>(state => state.IsConsistent);
        }

        private readonly DirectoryInfo location;
        private readonly AsyncManualResetEvent commitEvent;
        private readonly LockManager syncRoot;
        private readonly LogEntry initialEntry;
        private readonly long initialSize;
        private readonly MemoryAllocator<LogEntry>? entryPool;
        private readonly MemoryAllocator<LogEntryMetadata>? metadataPool;
        private readonly int bufferSize, snapshotBufferSize;
        private readonly bool replayOnInitialize, automaticCompaction, writeThrough;

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
            configuration ??= new Options();
            if (recordsPerPartition < 2L)
                throw new ArgumentOutOfRangeException(nameof(recordsPerPartition));
            if (!path.Exists)
                path.Create();
            writeThrough = configuration.WriteThrough;
            automaticCompaction = configuration.CompactionMode == CompactionMode.Foreground;
            backupCompression = configuration.BackupCompression;
            replayOnInitialize = configuration.ReplayOnInitialize;
            bufferSize = configuration.BufferSize;
            snapshotBufferSize = configuration.SnapshotBufferSize;
            location = path;
            this.recordsPerPartition = recordsPerPartition;
            initialSize = configuration.InitialPartitionSize;
            commitEvent = new AsyncManualResetEvent(false);
            sessionManager = new DataAccessSessionManager(configuration.MaxConcurrentReads, configuration.GetMemoryAllocator<byte>(), bufferSize);
            syncRoot = new LockManager(sessionManager.Capacity);
            entryPool = configuration.GetMemoryAllocator<LogEntry>();
            metadataPool = configuration.UseCaching ? configuration.GetMemoryAllocator<LogEntryMetadata>() : null;
            initialEntry = new LogEntry(sessionManager.WriteSession.Buffer);

            var partitionTable = new SortedSet<Partition>(Comparer<Partition>.Create(ComparePartitions));

            // load all partitions from file system
            foreach (var file in path.EnumerateFiles())
            {
                if (long.TryParse(file.Name, out var partitionNumber))
                {
                    var partition = new Partition(file.Directory!, bufferSize, recordsPerPartition, partitionNumber, metadataPool, sessionManager.Capacity, writeThrough);
                    partition.PopulateCache(sessionManager.WriteSession);
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
            state = new NodeState(path, syncRoot);
            snapshot = new Snapshot(path, snapshotBufferSize, sessionManager.Capacity, writeThrough);
            snapshot.PopulateCache(sessionManager.WriteSession);

            static int ComparePartitions(Partition x, Partition y)
            {
                var xn = x.PartitionNumber;
                return xn.CompareTo(y.PartitionNumber);
            }
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
        /// Gets compaction mode.
        /// </summary>
        public CompactionMode Compaction => automaticCompaction ? CompactionMode.Foreground : CompactionMode.Background;

        /// <inheritdoc/>
        bool IAuditTrail.IsLogEntryLengthAlwaysPresented => true;

        /// <summary>
        /// Gets the buffer that can be used to perform I/O operations.
        /// </summary>
        /// <remarks>
        /// This property always throws <see cref="InvalidOperationException"/>.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Attempt to obtain buffer without synchronization.</exception>
        [Obsolete("This buffer should not be shared between SnapshotBuilder and overridden AppendAsync method. Use your own separated buffers.", true)]
        protected Memory<byte> Buffer => throw new InvalidOperationException();

        private Partition CreatePartition(long partitionNumber)
        {
            var result = new Partition(location, bufferSize, recordsPerPartition, partitionNumber, metadataPool, sessionManager.Capacity, writeThrough);
            result.Allocate(initialSize);
            return result;
        }

        private async ValueTask<TResult> UnsafeReadAsync<TResult>(LogEntryConsumer<IRaftLogEntry, TResult> reader, DataAccessSession session, long startIndex, long endIndex, CancellationToken token)
        {
            if (startIndex > state.LastIndex)
                throw new IndexOutOfRangeException(ExceptionMessages.InvalidEntryIndex(endIndex));
            if (endIndex > state.LastIndex)
                throw new IndexOutOfRangeException(ExceptionMessages.InvalidEntryIndex(endIndex));
            var length = endIndex - startIndex + 1L;
            if (length > int.MaxValue)
                throw new InternalBufferOverflowException(ExceptionMessages.RangeTooBig);
            LogEntry entry;
            ValueTask<TResult> result;
            if (HasPartitions)
            {
                using var list = entryPool.Invoke((int)length, true);
                var listIndex = 0;
                for (Partition? partition = null; startIndex <= endIndex; list[listIndex++] = entry, startIndex++)
                {
                    if (startIndex > 0L && TryGetPartition(startIndex, ref partition, out var switched))
                    {
                        // handle regular record
                        entry = await partition.ReadAsync(session, startIndex, true, switched, token).ConfigureAwait(false);
                    }
                    else if (snapshot.Length > 0 && startIndex <= snapshot.Index)
                    {
                        // probably the record is snapshotted
                        entry = await snapshot.ReadAsync(session, token).ConfigureAwait(false);

                        // skip squashed log entries
                        startIndex = snapshot.Index;

                        // reset search hint
                        partition = null;
                    }
                    else
                    {
                        Debug.Assert(startIndex == 0L);

                        // handle ephemeral entity
                        entry = initialEntry;
                    }
                }

                return await reader.ReadAsync<LogEntry, InMemoryList<LogEntry>>(list.Memory.Slice(0, listIndex), list[0].SnapshotIndex, token).ConfigureAwait(false);
            }
            else if (snapshot.Length > 0)
            {
                entry = await snapshot.ReadAsync(session, token).ConfigureAwait(false);
                result = reader.ReadAsync<LogEntry, SingletonEntryList<LogEntry>>(new SingletonEntryList<LogEntry>(entry), entry.SnapshotIndex, token);
            }
            else
            {
                result = startIndex == 0L ? reader.ReadAsync<LogEntry, SingletonEntryList<LogEntry>>(new SingletonEntryList<LogEntry>(initialEntry), null, token) : reader.ReadAsync<LogEntry, LogEntry[]>(Array.Empty<LogEntry>(), null, token);
            }

            return await result.ConfigureAwait(false);
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
        public async ValueTask<TResult> ReadAsync<TResult>(LogEntryConsumer<IRaftLogEntry, TResult> reader, long startIndex, long endIndex, CancellationToken token)
        {
            if (startIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            if (endIndex < 0L)
                throw new ArgumentOutOfRangeException(nameof(endIndex));
            if (endIndex < startIndex)
                return await reader.ReadAsync<LogEntry, LogEntry[]>(Array.Empty<LogEntry>(), null, token).ConfigureAwait(false);

            // obtain weak lock as read lock
            await syncRoot.AcquireReadLockAsync(token).ConfigureAwait(false);
            var session = sessionManager.OpenSession(bufferSize);
            try
            {
                return await UnsafeReadAsync(reader, session, startIndex, endIndex, token).ConfigureAwait(false);
            }
            finally
            {
                sessionManager.CloseSession(session);  // return session back to the pool
                syncRoot.ReleaseReadLock();
            }
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

        private async ValueTask<TResult> ReadAsyncCore<TResult>(LogEntryConsumer<IRaftLogEntry, TResult> reader, long startIndex, CancellationToken token)
        {
            await syncRoot.AcquireReadLockAsync(token).ConfigureAwait(false);
            var session = sessionManager.OpenSession(bufferSize);
            try
            {
                return await UnsafeReadAsync(reader, session, startIndex, state.LastIndex, token).ConfigureAwait(false);
            }
            finally
            {
                sessionManager.CloseSession(session);
                syncRoot.ReleaseReadLock();
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
        public ValueTask<TResult> ReadAsync<TResult>(LogEntryConsumer<IRaftLogEntry, TResult> reader, long startIndex, CancellationToken token)
        {
            ValueTask<TResult> result;
            if (startIndex < 0L)
                result = new ValueTask<TResult>(Task.FromException<TResult>(new ArgumentOutOfRangeException(nameof(startIndex))));
            else if (startIndex <= state.LastIndex)
                result = ReadAsyncCore(reader, startIndex, token);
            else
                result = reader.ReadAsync<IRaftLogEntry, IRaftLogEntry[]>(Array.Empty<IRaftLogEntry>(), null, token);

            return result;
        }

        private async ValueTask UnsafeInstallSnapshotAsync<TSnapshot>(TSnapshot snapshot, long snapshotIndex)
            where TSnapshot : notnull, IRaftLogEntry
        {
            // 0. The snapshot can be installed only if the partitions were squashed on the sender side
            // therefore, snapshotIndex should be a factor of recordsPerPartition
            if ((snapshotIndex + 1) % recordsPerPartition != 0)
                throw new ArgumentOutOfRangeException(nameof(snapshotIndex));

            // 1. Save the snapshot into temporary file to avoid corruption caused by network connection
            string tempSnapshotFile, snapshotFile = this.snapshot.FileName;
            await using (var tempSnapshot = new Snapshot(location, snapshotBufferSize, 0, writeThrough, true))
            {
                tempSnapshotFile = tempSnapshot.FileName;
                await tempSnapshot.WriteAsync(sessionManager.WriteSession, snapshot, snapshotIndex, CancellationToken.None).ConfigureAwait(false);
            }

            // 2. Delete existing snapshot file
            await this.snapshot.DisposeAsync().ConfigureAwait(false);

            /*
             * Swapping snapshot file is unsafe operation because of potential disk I/O failures.
             * However, event if swapping will fail then it can be recovered manually just by renaming 'snapshot.new' file
             * into 'snapshot'. Both versions of snapshot file stay consistent. That's why stream copying is not an option.
             */
            try
            {
                File.Delete(snapshotFile);
                File.Move(tempSnapshotFile, snapshotFile);
            }
            catch (Exception e)
            {
                Environment.FailFast(LogMessages.SnapshotInstallationFailed, e);
            }

            this.snapshot = new Snapshot(location, snapshotBufferSize, sessionManager.Capacity, writeThrough);
            this.snapshot.PopulateCache(sessionManager.WriteSession);

            // execute deletion of replaced partitions and snapshot installation in parallel
            await Task.WhenAll(ApplySnapshotAsync(snapshotIndex, snapshot.Term), RemovePartitionsAsync(snapshotIndex)).ConfigureAwait(false);
            commitEvent.Set(true);

            async Task RemovePartitionsAsync(long snapshotIndex)
            {
                // 3. Identify all partitions to be replaced by snapshot and delete them
                for (Partition? current = head, next; current is not null; current = next)
                {
                    next = current.Next;
                    if (current.LastIndex <= snapshotIndex)
                    {
                        await RemovePartitionAsync(current).ConfigureAwait(false);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // 5. Apply snapshot to the underlying state machine
            async Task ApplySnapshotAsync(long snapshotIndex, long snapshotTerm)
            {
                state.CommitIndex = snapshotIndex;
                state.LastIndex = Math.Max(snapshotIndex, state.LastIndex);

                await ApplyAsync(await this.snapshot.ReadAsync(sessionManager.WriteSession, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
                lastTerm.VolatileWrite(snapshotTerm);
                state.LastApplied = snapshotIndex;
                state.Flush();
                await FlushAsync().ConfigureAwait(false);
            }
        }

        private async ValueTask UnsafeAppendAsync<TEntry>(ILogEntryProducer<TEntry> supplier, long startIndex, bool skipCommitted, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry
        {
            if (startIndex > state.LastIndex + 1)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            Partition? partition;
            for (partition = null; !token.IsCancellationRequested && await supplier.MoveNextAsync().ConfigureAwait(false); state.LastIndex = startIndex++)
            {
                if (supplier.Current.IsSnapshot)
                    throw new InvalidOperationException(ExceptionMessages.SnapshotDetected);

                if (startIndex > state.CommitIndex)
                {
                    await GetOrCreatePartitionAsync(startIndex, ref partition).ConfigureAwait(false);
                    await partition.WriteAsync(sessionManager.WriteSession, supplier.Current, startIndex).ConfigureAwait(false);
                }
                else if (!skipCommitted)
                {
                    throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
                }
            }

            if (partition is not null)
                await partition.FlushAsync().ConfigureAwait(false);

            // flush updated state
            state.Flush();
            token.ThrowIfCancellationRequested();
        }

        /// <inheritdoc/>
        async ValueTask IAuditTrail<IRaftLogEntry>.AppendAsync<TEntry>(ILogEntryProducer<TEntry> entries, long startIndex, bool skipCommitted, CancellationToken token)
        {
            if (entries.RemainingCount == 0L)
                return;
            await syncRoot.AcquireWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                await UnsafeAppendAsync(entries, startIndex, skipCommitted, token).ConfigureAwait(false);
            }
            finally
            {
                syncRoot.ReleaseWriteLock();
            }
        }

        private async ValueTask UnsafeAppendAsync<TEntry>(TEntry entry, long startIndex)
            where TEntry : notnull, IRaftLogEntry
        {
            if (startIndex <= state.CommitIndex)
                throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
            if (startIndex > state.LastIndex + 1L)
                throw new ArgumentOutOfRangeException(nameof(startIndex));

            Partition? partition = null;
            GetOrCreatePartition(startIndex, ref partition);
            await partition.WriteAsync(sessionManager.WriteSession, entry, startIndex).ConfigureAwait(false);
            await partition.FlushAsync().ConfigureAwait(false);
            state.LastIndex = startIndex;
            state.Flush();
        }

        private async ValueTask<long> UnsafeAppendAsync<TEntry>(TEntry entry, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry
        {
            var startIndex = state.LastIndex + 1L;
            Partition? partition = null;
            GetOrCreatePartition(startIndex, ref partition);
            await partition.WriteAsync(sessionManager.WriteSession, entry, startIndex).ConfigureAwait(false);
            await partition.FlushAsync(token).ConfigureAwait(false);
            state.LastIndex = startIndex;
            state.Flush();
            return startIndex;
        }

        /// <summary>
        /// Adds uncommitted log entry to the end of this log.
        /// </summary>
        /// <typeparam name="TEntry">The actual type of the supplied log entry.</typeparam>
        /// <param name="writeLock">The acquired lock token.</param>
        /// <param name="entry">The uncommitted log entry to be added into this audit trail.</param>
        /// <param name="startIndex">The index from which all previous log entries should be dropped and replaced with the new entry.</param>
        /// <returns>The task representing asynchronous state of the method.</returns>
        /// <exception cref="ArgumentException"><paramref name="writeLock"/> is invalid.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="startIndex"/> is less than the index of the last committed entry; or <paramref name="entry"/> is the snapshot.</exception>
        public ValueTask AppendAsync<TEntry>(in WriteLockToken writeLock, TEntry entry, long startIndex)
            where TEntry : notnull, IRaftLogEntry
        {
            ValueTask result;
            if (entry.IsSnapshot)
            {
                result = new ValueTask(Task.FromException(new InvalidOperationException(ExceptionMessages.SnapshotDetected)));
            }
            else if (Validate(in writeLock))
            {
                result = UnsafeAppendAsync(entry, startIndex);
            }
            else
            {
                result = new ValueTask(Task.FromException(new ArgumentException(ExceptionMessages.InvalidLockToken, nameof(writeLock))));
            }

            return result;
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
        /// <returns>The task representing asynchronous state of the method.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="startIndex"/> is less than the index of the last committed entry and <paramref name="entry"/> is not a snapshot.</exception>
        public ValueTask AppendAsync<TEntry>(TEntry entry, long startIndex)
            where TEntry : notnull, IRaftLogEntry
        {
            return entry.IsSnapshot ? InstallSnapshotAsync(entry, startIndex) : AppendRegularEntryAsync(entry, startIndex);

            async ValueTask AppendRegularEntryAsync(TEntry entry, long startIndex)
            {
                Debug.Assert(!entry.IsSnapshot);
                await syncRoot.AcquireWriteLockAsync().ConfigureAwait(false);
                try
                {
                    await UnsafeAppendAsync(entry, startIndex).ConfigureAwait(false);
                }
                finally
                {
                    syncRoot.ReleaseWriteLock();
                }
            }

            async ValueTask InstallSnapshotAsync(TEntry entry, long startIndex)
            {
                Debug.Assert(entry.IsSnapshot);

                // Snapshot requires exclusive lock. However, snapshot installation is very rare operation
                await syncRoot.AcquireExclusiveLockAsync().ConfigureAwait(false);
                try
                {
                    if (startIndex <= state.CommitIndex)
                        throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
                    await UnsafeInstallSnapshotAsync(entry, startIndex).ConfigureAwait(false);
                }
                finally
                {
                    syncRoot.ReleaseExclusiveLock();
                }
            }
        }

        /// <summary>
        /// Adds uncommitted log entry to the end of this log.
        /// </summary>
        /// <remarks>
        /// This method cannot be used to append a snapshot.
        /// </remarks>
        /// <param name="writeLock">The acquired lock token.</param>
        /// <param name="entry">The entry to add.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <typeparam name="TEntry">The actual type of the supplied log entry.</typeparam>
        /// <returns>The index of the added entry.</returns>
        /// <exception cref="ArgumentException"><paramref name="writeLock"/> is invalid.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="entry"/> is the snapshot entry.</exception>
        public ValueTask<long> AppendAsync<TEntry>(in WriteLockToken writeLock, TEntry entry, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry
        {
            ValueTask<long> result;
            if (entry.IsSnapshot)
            {
                result = new ValueTask<long>(Task.FromException<long>(new InvalidOperationException(ExceptionMessages.SnapshotDetected)));
            }
            else if (Validate(in writeLock))
            {
                result = UnsafeAppendAsync(entry, token);
            }
            else
            {
                result = new ValueTask<long>(Task.FromException<long>(new ArgumentException(ExceptionMessages.InvalidLockToken, nameof(writeLock))));
            }

            return result;
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
        public async ValueTask<long> AppendAsync<TEntry>(TEntry entry, CancellationToken token = default)
            where TEntry : notnull, IRaftLogEntry
        {
            if (entry.IsSnapshot)
                throw new InvalidOperationException(ExceptionMessages.SnapshotDetected);
            await syncRoot.AcquireWriteLockAsync(token).ConfigureAwait(false);
            long startIndex;
            try
            {
                startIndex = await UnsafeAppendAsync(entry, token).ConfigureAwait(false);
            }
            finally
            {
                syncRoot.ReleaseWriteLock();
            }

            return startIndex;
        }

        /// <summary>
        /// Adds uncommitted log entries to the end of this log.
        /// </summary>
        /// <remarks>
        /// This method should updates cached value provided by method <see cref="IAuditTrail.GetLastIndex"/> called with argument of value <see langword="false"/>.
        /// </remarks>
        /// <typeparam name="TEntry">The actual type of the log entry returned by the supplier.</typeparam>
        /// <param name="entries">The entries to be added into this log.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>Index of the first added entry.</returns>
        /// <exception cref="ArgumentException"><paramref name="entries"/> is empty.</exception>
        /// <exception cref="InvalidOperationException">The collection of entries contains the snapshot entry.</exception>
        public async ValueTask<long> AppendAsync<TEntry>(ILogEntryProducer<TEntry> entries, CancellationToken token = default)
            where TEntry : notnull, IRaftLogEntry
        {
            if (entries.RemainingCount == 0L)
                throw new ArgumentException(ExceptionMessages.EntrySetIsEmpty);
            await syncRoot.AcquireWriteLockAsync(token).ConfigureAwait(false);
            var startIndex = state.LastIndex + 1L;
            try
            {
                await UnsafeAppendAsync(entries, startIndex, false, token).ConfigureAwait(false);
            }
            finally
            {
                syncRoot.ReleaseWriteLock();
            }

            return startIndex;
        }

        /// <summary>
        /// Dropes the uncommitted entries starting from the specified position to the end of the log.
        /// </summary>
        /// <param name="startIndex">The index of the first log entry to be dropped.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The actual number of dropped entries.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="startIndex"/> represents index of the committed entry.</exception>
        public async ValueTask<long> DropAsync(long startIndex, CancellationToken token)
        {
            long count = 0L;
            if (startIndex > state.LastIndex)
                goto exit;

            await syncRoot.AcquireWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                if (startIndex <= state.CommitIndex)
                    throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
                count = state.LastIndex - startIndex + 1L;
                state.LastIndex = startIndex - 1L;
                state.Flush();

                // find partitions to be deleted
                var partitionNumber = Math.DivRem(startIndex, recordsPerPartition, out var remainder);

                // take the next partition if startIndex is not a beginning of the calculated partition
                partitionNumber += (remainder > 0L).ToInt32();
                for (Partition? partition = TryGetPartition(partitionNumber), next; partition is not null; partition = next)
                {
                    next = partition.Next;
                    await RemovePartitionAsync(partition).ConfigureAwait(false);
                }
            }
            finally
            {
                syncRoot.ReleaseWriteLock();
            }

            exit:
            return count;
        }

        /// <summary>
        /// Waits for the commit.
        /// </summary>
        /// <param name="timeout">The timeout used to wait for the commit.</param>
        /// <param name="token">The token that can be used to cancel waiting.</param>
        /// <returns><see langword="true"/> if log entry is committed; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task<bool> WaitForCommitAsync(TimeSpan timeout, CancellationToken token)
            => commitEvent.WaitAsync(timeout, token);

        /// <summary>
        /// Waits for specific commit.
        /// </summary>
        /// <param name="index">The index of the log record to be committed.</param>
        /// <param name="timeout">The timeout used to wait for the commit.</param>
        /// <param name="token">The token that can be used to cancel waiting.</param>
        /// <returns><see langword="true"/> if log entry is committed; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task<bool> WaitForCommitAsync(long index, TimeSpan timeout, CancellationToken token)
            => commitEvent.WaitForCommitAsync(NodeState.IsCommittedPredicate, state, index, timeout, token);

        private async ValueTask ForceCompactionAsync(long upperBoundIndex, SnapshotBuilder builder, CancellationToken token)
        {
            // 1. Find the partitions that can be compacted
            Partition? current, next;
            var lastPartition = -1L;
            for (current = head; current is not null; current = current.Next, token.ThrowIfCancellationRequested())
            {
                if (current.LastIndex <= upperBoundIndex)
                    lastPartition = current.PartitionNumber;
                else
                    break; // linked list is sorted by partition number so we don't need to enumerate over all partitions
            }

            LogEntry entry;

            // 2. Initialize builder with snapshot record
            if (snapshot.Length > 0L)
            {
                entry = await snapshot.ReadAsync(sessionManager.CompactionSession, token).ConfigureAwait(false);
                entry.Reset();
                await builder.ApplyCoreAsync(entry).ConfigureAwait(false);
            }

            // 3. Do compaction
            var snapshotIndex = 0L;
            for (current = head; current is not null && current.PartitionNumber <= lastPartition; current = current.Next)
            {
                await current.FlushAsync(sessionManager.CompactionSession, token).ConfigureAwait(false);
                for (var i = 0; i < current.Capacity; i++)
                {
                    entry = current.FirstIndex > 0L || i > 0L ?
                        await current.ReadAsync(sessionManager.CompactionSession, i, false, false, token).ConfigureAwait(false) :
                        initialEntry;
                    entry.Reset();
                    await builder.ApplyCoreAsync(entry).ConfigureAwait(false);
                }

                snapshotIndex = current.LastIndex;
            }

            // 4. Persist snapshot
            await snapshot.WriteAsync(sessionManager.CompactionSession, builder, snapshotIndex, token).ConfigureAwait(false);
            await snapshot.FlushAsync(token).ConfigureAwait(false);

            // 5. Remove snapshotted partitions
            for (current = head; current is not null && current.PartitionNumber <= lastPartition; current = next)
            {
                next = current.Next;
                await RemovePartitionAsync(current).ConfigureAwait(false);
            }
        }

        private bool IsCompactionRequired(long upperBoundIndex)
            => upperBoundIndex - snapshot.Index >= recordsPerPartition;

        private async ValueTask ForceBackgroundCompactionAsync(long upperBoundIndex, CancellationToken token)
        {
            SnapshotBuilder? builder;
            if (IsCompactionRequired(upperBoundIndex) && (builder = CreateSnapshotBuilder()) is not null)
            {
                await syncRoot.AcquireCompactionLockAsync(token).ConfigureAwait(false);
                try
                {
                    // check compaction range again because snapshot index can be modified by snapshot installation method
                    if (IsCompactionRequired(upperBoundIndex))
                        await ForceCompactionAsync(upperBoundIndex, builder, token).ConfigureAwait(false);
                }
                finally
                {
                    syncRoot.ReleaseCompactionLock();
                    builder.Dispose();
                }
            }
        }

        private async ValueTask ForceForegroundCompactionAsync(long upperBoundIndex, CancellationToken token)
        {
            SnapshotBuilder? builder;
            if (IsCompactionRequired(upperBoundIndex) && (builder = CreateSnapshotBuilder()) is not null)
            {
                using (builder)
                {
                    await ForceCompactionAsync(upperBoundIndex, builder, token).ConfigureAwait(false);
                }
            }
        }

        // In case of background compaction we need to have 1 fully committed partition as a divider
        // between partitions produced during writes and partitions to be compacted.
        // This restriction guarantees that compaction and writer thread will not be concurrent
        // when modifying Partition.next and Partition.previous fields need to keep sorted linked list
        // consistent and sorted.
        private long BackgroundCompactionCount => Math.Max(((state.LastApplied - snapshot.Index) / recordsPerPartition) - 1L, 0L);

        private long ForegroundCompactionCount => Math.Max((state.CommitIndex - snapshot.Index) / recordsPerPartition, 0L);

        /// <summary>
        /// Gets approximate number of partitions that can be compacted.
        /// </summary>
        public long CompactionCount => automaticCompaction ? ForegroundCompactionCount : BackgroundCompactionCount;

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
                result = new ValueTask(DisposedTask);
            }
            else if (count < 0L)
            {
                result = new ValueTask(Task.FromException(new ArgumentOutOfRangeException(nameof(count))));
            }
            else if (count == 0L || automaticCompaction)
            {
                result = new ValueTask();
            }
            else
            {
                // convert count to the record index
                count = checked(recordsPerPartition * Math.Min(count, BackgroundCompactionCount));
                result = ForceBackgroundCompactionAsync(count, token);
            }

            return result;
        }

        private ValueTask<long> CommitAsync(long? endIndex, CancellationToken token)
        {
            // exclusive lock is required for foreground compaction;
            // otherwise - write lock which doesn't block background compaction
            return automaticCompaction ? CommitAndCompactInForegroundAsync(endIndex, token) : CommitAndCompactInBackgroundAsync(endIndex, token);

            async ValueTask<long> CommitAndCompactInForegroundAsync(long? endIndex, CancellationToken token)
            {
                long count;
                await syncRoot.AcquireExclusiveLockAsync(token).ConfigureAwait(false);
                var startIndex = state.CommitIndex + 1L;
                try
                {
                    count = endIndex.HasValue ? Math.Min(state.LastIndex, endIndex.GetValueOrDefault()) : state.LastIndex;
                    count = count - startIndex + 1L;
                    if (count > 0)
                    {
                        state.CommitIndex = startIndex = startIndex + count - 1;
                        await ApplyAsync(token).ConfigureAwait(false);
                        await ForceForegroundCompactionAsync(startIndex, token).ConfigureAwait(false);
                    }
                }
                finally
                {
                    syncRoot.ReleaseExclusiveLock();
                }

                count = Math.Max(count, 0L);
                if (count > 0L)
                    commitEvent.Set(true);

                return count;
            }

            async ValueTask<long> CommitAndCompactInBackgroundAsync(long? endIndex, CancellationToken token)
            {
                long count;
                await syncRoot.AcquireWriteLockAsync(token).ConfigureAwait(false);
                var startIndex = state.CommitIndex + 1L;
                try
                {
                    count = endIndex.HasValue ? Math.Min(state.LastIndex, endIndex.GetValueOrDefault()) : state.LastIndex;
                    count = count - startIndex + 1L;
                    if (count > 0)
                    {
                        state.CommitIndex = startIndex = startIndex + count - 1;
                        await ApplyAsync(token).ConfigureAwait(false);
                    }
                }
                finally
                {
                    syncRoot.ReleaseWriteLock();
                }

                count = Math.Max(count, 0L);
                if (count > 0L)
                    commitEvent.Set(true);

                return count;
            }
        }

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
        public ValueTask<long> CommitAsync(long endIndex, CancellationToken token) => CommitAsync(new long?(endIndex), token);

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
        public ValueTask<long> CommitAsync(CancellationToken token) => CommitAsync(null, token);

        /// <summary>
        /// Applies the command represented by the log entry to the underlying database engine.
        /// </summary>
        /// <param name="entry">The entry to be applied to the state machine.</param>
        /// <remarks>
        /// The base method does nothing so you don't need to call base implementation.
        /// </remarks>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <seealso cref="Commands.CommandInterpreter"/>
        protected virtual ValueTask ApplyAsync(LogEntry entry) => new ValueTask();

        /// <summary>
        /// Flushes the underlying data storage.
        /// </summary>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        protected virtual ValueTask FlushAsync() => new ValueTask();

        private async ValueTask ApplyAsync(long startIndex, CancellationToken token)
        {
            for (Partition? partition = null; startIndex <= state.CommitIndex; state.LastApplied = startIndex++)
            {
                if (TryGetPartition(startIndex, ref partition, out var switched))
                {
                    var entry = await partition.ReadAsync(sessionManager.WriteSession, startIndex, true, switched, token).ConfigureAwait(false);
                    entry.Reset();
                    await ApplyAsync(entry).ConfigureAwait(false);
                    lastTerm.VolatileWrite(entry.Term);
                }
                else
                {
                    throw new MissingPartitionException(startIndex);
                }
            }

            state.Flush();
            await FlushAsync().ConfigureAwait(false);
        }

        private ValueTask ApplyAsync(CancellationToken token)
            => ApplyAsync(state.LastApplied + 1L, token);

        /// <summary>
        /// Reconstructs dataset by calling <see cref="ApplyAsync(LogEntry)"/>
        /// for each committed entry.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the method.</returns>
        public async Task ReplayAsync(CancellationToken token = default)
        {
            await syncRoot.AcquireExclusiveLockAsync(token).ConfigureAwait(false);
            try
            {
                LogEntry entry;
                long startIndex;

                // 1. Apply snapshot if it not empty
                if (snapshot.Length > 0L)
                {
                    entry = await snapshot.ReadAsync(sessionManager.WriteSession, token).ConfigureAwait(false);
                    await ApplyAsync(entry).ConfigureAwait(false);
                    lastTerm.VolatileWrite(entry.Term);
                    startIndex = snapshot.Index;
                }
                else
                {
                    startIndex = 0L;
                }

                // 2. Apply all committed entries
                await ApplyAsync(startIndex + 1L, token).ConfigureAwait(false);
            }
            finally
            {
                syncRoot.ReleaseExclusiveLock();
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
            Task result;
            if (token.IsCancellationRequested)
                result = Task.FromCanceled(token);
            else if (replayOnInitialize)
                result = ReplayAsync(token);
            else
                result = Task.CompletedTask;

            return result;
        }

        private bool IsConsistent => state.Term <= lastTerm.VolatileRead();

        private async Task EnsureConsistencyImpl(TimeSpan timeout, CancellationToken token)
        {
            for (var timeoutTracker = new Timeout(timeout); !IsConsistent; await commitEvent.WaitAsync(IsConsistentPredicate, this, timeout, token).ConfigureAwait(false))
                timeoutTracker.ThrowIfExpired(out timeout);
        }

        /// <summary>
        /// Suspens the caller until the log entry with term equal to <see cref="Term"/>
        /// will be committed.
        /// </summary>
        /// <param name="timeout">The time to wait.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of the asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="TimeoutException">Timeout occurred.</exception>
        public Task EnsureConsistencyAsync(TimeSpan timeout, CancellationToken token)
            => IsConsistent ? Task.CompletedTask : EnsureConsistencyImpl(timeout, token);

        /// <inheritdoc/>
        bool IPersistentState.IsVotedFor(IRaftClusterMember? member) => state.IsVotedFor(member?.Id);

        /// <summary>
        /// Gets the current term.
        /// </summary>
        public long Term => state.Term;

        /// <inheritdoc/>
        ValueTask<long> IPersistentState.IncrementTermAsync() => state.IncrementTermAsync();

        /// <inheritdoc/>
        ValueTask IPersistentState.UpdateTermAsync(long term) => state.UpdateTermAsync(term);

        /// <inheritdoc/>
        ValueTask IPersistentState.UpdateVotedForAsync(IRaftClusterMember? member) => state.UpdateVotedForAsync(member?.Id);

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
                sessionManager.Dispose();
                state.Dispose();
                commitEvent.Dispose();
                syncRoot.Dispose();
                snapshot.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <inheritdoc />
        protected override async ValueTask DisposeAsyncCore()
        {
            for (Partition? current = head, next; current is not null; current = next)
            {
                next = current.Next;
                await current.DisposeAsync().ConfigureAwait(false);
            }

            head = tail = null;
            sessionManager.Dispose();
            state.Dispose();
            commitEvent.Dispose();
            syncRoot.Dispose();
            await snapshot.DisposeAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Releases resources associated with this persistent storage asynchronously.
        /// </summary>
        /// <returns>A task representing state of asynchronous execution.</returns>
        public ValueTask DisposeAsync() => DisposeAsync(false);
    }
}
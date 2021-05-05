using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Buffers;
    using Collections.Specialized;
    using IO.Log;
    using Replication;
    using static IO.DataTransferObject;
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
            IsConsistentPredicate = IsConsistentCore;

            static bool IsConsistentCore(PersistentState state) => state.IsConsistent;
        }

        private readonly DirectoryInfo location;
        private readonly AsyncManualResetEvent commitEvent;
        private readonly LockManager syncRoot;
        private readonly LogEntry initialEntry;
        private readonly long initialSize;
        private readonly BufferManager bufferManager;
        private readonly int bufferSize, snapshotBufferSize;
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
            sessionManager = new(configuration.MaxConcurrentReads, bufferManager.BufferAllocator, bufferSize);
            syncRoot = new(configuration);
            initialEntry = new(sessionManager.WriteSession.Buffer);
            evictOnCommit = configuration.CacheEvictionPolicy == LogEntryCacheEvictionPolicy.OnCommit;

            var partitionTable = new SortedSet<Partition>(Comparer<Partition>.Create(ComparePartitions));

            // load all partitions from file system
            foreach (var file in path.EnumerateFiles())
            {
                if (long.TryParse(file.Name, out var partitionNumber))
                {
                    var partition = new Partition(file.Directory!, bufferSize, recordsPerPartition, partitionNumber, in bufferManager, sessionManager.Capacity, writeThrough, initialSize);
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
            snapshot = new(path, snapshotBufferSize, sessionManager.Capacity, writeThrough);
            snapshot.Initialize();

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

        /// <summary>
        /// Gets the buffer that can be used to perform I/O operations.
        /// </summary>
        /// <remarks>
        /// This property throws <see cref="InvalidOperationException"/> if
        /// the configured compaction mode is not <see cref="CompactionMode.Sequential"/>.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Attempt to obtain buffer without synchronization.</exception>
        [Obsolete("This property available only if Sequential log compaction is in use. Use your own separated buffers.", true)]
        protected Memory<byte> Buffer
            => compaction == CompactionMode.Sequential ? sessionManager.WriteSession.Buffer : throw new InvalidOperationException();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Partition CreatePartition(long partitionNumber)
            => new Partition(location, bufferSize, recordsPerPartition, partitionNumber, in bufferManager, sessionManager.Capacity, writeThrough, initialSize);

        private ValueTask<TResult> UnsafeReadAsync<TResult>(LogEntryConsumer<IRaftLogEntry, TResult> reader, DataAccessSession session, long startIndex, long endIndex, CancellationToken token)
        {
            if (startIndex > state.LastIndex)
#if NETSTANDARD2_1
                return new (Task.FromException<TResult>(new IndexOutOfRangeException(ExceptionMessages.InvalidEntryIndex(startIndex))));
#else
                return ValueTask.FromException<TResult>(new IndexOutOfRangeException(ExceptionMessages.InvalidEntryIndex(startIndex)));
#endif

            if (endIndex > state.LastIndex)
#if NETSTANDARD2_1
                return new (Task.FromException<TResult>(new IndexOutOfRangeException(ExceptionMessages.InvalidEntryIndex(endIndex))));
#else
                return ValueTask.FromException<TResult>(new IndexOutOfRangeException(ExceptionMessages.InvalidEntryIndex(endIndex)));
#endif

            var length = endIndex - startIndex + 1L;
            if (length > int.MaxValue)
#if NETSTANDARD2_1
                return new (Task.FromException<TResult>(new InternalBufferOverflowException(ExceptionMessages.RangeTooBig)));
#else
                return ValueTask.FromException<TResult>(new InternalBufferOverflowException(ExceptionMessages.RangeTooBig));
#endif

            readCounter?.Invoke(length);
            if (HasPartitions)
                return ReadEntriesAsync(reader, session, startIndex, endIndex, (int)length, token);

            if (!snapshot.IsEmpty)
                return ReadSnapshotAsync(reader, session, token);

            return ReadInitialOrEmptyEntryAsync(in reader, startIndex == 0L, token);

            async ValueTask<TResult> ReadEntriesAsync(LogEntryConsumer<IRaftLogEntry, TResult> reader, DataAccessSession session, long startIndex, long endIndex, int length, CancellationToken token)
            {
                using var list = bufferManager.AllocLogEntryList(length);
                Debug.Assert(list.Length >= length);

                // try to read snapshot out of the loop
                if (!snapshot.IsEmpty && startIndex <= snapshot.Index)
                {
                    var snapshotEntry = await snapshot.ReadAsync(session, token).ConfigureAwait(false);
                    BufferHelpers.GetReference(in list) = snapshotEntry;

                    // skip squashed log entries
                    startIndex = snapshot.Index + 1L;
                    length = 1;
                }
                else if (startIndex == 0L)
                {
                    BufferHelpers.GetReference(in list) = initialEntry;
                    startIndex = length = 1;
                }
                else
                {
                    length = 0;
                }

                return await ReadEntries(in reader, in list, startIndex, endIndex, length, token).ConfigureAwait(false);
            }

            ValueTask<TResult> ReadEntries(in LogEntryConsumer<IRaftLogEntry, TResult> reader, in MemoryOwner<LogEntry> list, long startIndex, long endIndex, int listIndex, CancellationToken token)
            {
                LogEntry entry;
                ref var first = ref BufferHelpers.GetReference(in list);

                // enumerate over partitions in search of log entries
                for (Partition? partition = null; startIndex <= endIndex && TryGetPartition(startIndex, ref partition); Unsafe.Add(ref first, listIndex++) = entry, startIndex++, token.ThrowIfCancellationRequested())
                    entry = partition.Read(session, startIndex, true);

                return reader.ReadAsync<LogEntry, InMemoryList<LogEntry>>(list.Memory.Slice(0, listIndex), first.SnapshotIndex, token);
            }

            async ValueTask<TResult> ReadSnapshotAsync(LogEntryConsumer<IRaftLogEntry, TResult> reader, DataAccessSession session, CancellationToken token)
            {
                var entry = await snapshot.ReadAsync(session, token).ConfigureAwait(false);
                return await reader.ReadAsync<LogEntry, SingletonEntryList<LogEntry>>(new(entry), entry.SnapshotIndex, token).ConfigureAwait(false);
            }

            ValueTask<TResult> ReadInitialOrEmptyEntryAsync(in LogEntryConsumer<IRaftLogEntry, TResult> reader, bool readEphemeralEntry, CancellationToken token)
                => readEphemeralEntry ? reader.ReadAsync<LogEntry, SingletonEntryList<LogEntry>>(new(initialEntry), null, token) : reader.ReadAsync<LogEntry, LogEntry[]>(Array.Empty<LogEntry>(), null, token);
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
        public ValueTask<TResult> ReadAsync<TResult>(LogEntryConsumer<IRaftLogEntry, TResult> reader, long startIndex, long endIndex, CancellationToken token)
        {
            ValueTask<TResult> result;
            if (IsDisposed)
                result = new(GetDisposedTask<TResult>());
            else if (startIndex < 0L)
#if NETSTANDARD2_1
                result = new (Task.FromException<TResult>(new ArgumentOutOfRangeException(nameof(startIndex))));
#else
                result = ValueTask.FromException<TResult>(new ArgumentOutOfRangeException(nameof(startIndex)));
#endif
            else if (endIndex < 0L)
#if NETSTANDARD2_1
                result = new (Task.FromException<TResult>(new ArgumentOutOfRangeException(nameof(endIndex))));
#else
                result = ValueTask.FromException<TResult>(new ArgumentOutOfRangeException(nameof(endIndex)));
#endif
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
            await syncRoot.AcquireReadLockAsync(token).ConfigureAwait(false);
            var session = sessionManager.OpenSession();
            try
            {
                return await UnsafeReadAsync(reader, session, startIndex, endIndex ?? state.LastIndex, token).ConfigureAwait(false);
            }
            finally
            {
                sessionManager.CloseSession(session);
                syncRoot.ReleaseReadLock();
            }
        }

        // buffered read
        private async ValueTask<TResult> ReadBufferedAsync<TResult>(LogEntryConsumer<IRaftLogEntry, TResult> reader, long startIndex, long? endIndex, CancellationToken token)
        {
            Debug.Assert(bufferingConsumer is not null);

            // create buffered copy of all entries
            BufferedRaftLogEntryList bufferedEntries;
            long? snapshotIndex;
            await syncRoot.AcquireReadLockAsync(token).ConfigureAwait(false);
            var session = sessionManager.OpenSession();
            try
            {
                (bufferedEntries, snapshotIndex) = await UnsafeReadAsync<(BufferedRaftLogEntryList, long?)>(new(bufferingConsumer), session, startIndex, endIndex ?? state.LastIndex, token).ConfigureAwait(false);
            }
            finally
            {
                sessionManager.CloseSession(session);
                syncRoot.ReleaseReadLock();
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
        public ValueTask<TResult> ReadAsync<TResult>(LogEntryConsumer<IRaftLogEntry, TResult> reader, long startIndex, CancellationToken token)
        {
            ValueTask<TResult> result;
            if (IsDisposed)
                result = new(GetDisposedTask<TResult>());
            else if (startIndex < 0L)
#if NETSTANDARD2_1
                result = new (Task.FromException<TResult>(new ArgumentOutOfRangeException(nameof(startIndex))));
#else
                result = ValueTask.FromException<TResult>(new ArgumentOutOfRangeException(nameof(startIndex)));
#endif
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
            // 1. Save the snapshot into temporary file to avoid corruption caused by network connection
            string tempSnapshotFile, snapshotFile = this.snapshot.FileName;
            await using (var tempSnapshot = new Snapshot(location, snapshotBufferSize, 0, writeThrough, true))
            {
                tempSnapshotFile = tempSnapshot.FileName;
                await tempSnapshot.WriteAsync(sessionManager.WriteSession, snapshot, snapshotIndex).ConfigureAwait(false);
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
#if NETSTANDARD2_1
                File.Delete(snapshotFile);
                File.Move(tempSnapshotFile, snapshotFile);
#else
                File.Move(tempSnapshotFile, snapshotFile, true);
#endif
            }
            catch (Exception e)
            {
                Environment.FailFast(LogMessages.SnapshotInstallationFailed, e);
            }

            Volatile.Write(ref this.snapshot, CreateSnapshot());

            // 5. Apply snapshot to the underlying state machine
            state.CommitIndex = snapshotIndex;
            state.LastIndex = Math.Max(snapshotIndex, state.LastIndex);
            await ApplyAsync(await this.snapshot.ReadAsync(sessionManager.WriteSession, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
            lastTerm.VolatileWrite(snapshot.Term);
            state.LastApplied = snapshotIndex;
            state.Flush();
            await FlushAsync().ConfigureAwait(false);
            commitEvent.Set(true);
            writeCounter?.Invoke(1D);
            return DetachPartitions(snapshotIndex);

            Snapshot CreateSnapshot()
            {
                var result = new Snapshot(location, snapshotBufferSize, sessionManager.Capacity, writeThrough);
                result.Initialize();
                return result;
            }
        }

        private async ValueTask UnsafeAppendAsync<TEntry>(ILogEntryProducer<TEntry> supplier, long startIndex, bool skipCommitted, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry
        {
            if (startIndex > state.LastIndex + 1)
                throw new ArgumentOutOfRangeException(nameof(startIndex));

            writeCounter?.Invoke(supplier.RemainingCount);
            for (Partition? partition = null; !token.IsCancellationRequested && await supplier.MoveNextAsync().ConfigureAwait(false); state.LastIndex = startIndex++)
            {
                if (supplier.Current.IsSnapshot)
                    throw new InvalidOperationException(ExceptionMessages.SnapshotDetected);

                if (startIndex > state.CommitIndex)
                {
                    GetOrCreatePartition(startIndex, ref partition);
                    await partition.WriteAsync(sessionManager.WriteSession, supplier.Current, startIndex).ConfigureAwait(false);

                    // flush if last entry is added to the partition or the last entry is consumed from the iterator
                    if (startIndex == partition.LastIndex || supplier.RemainingCount == 0L)
                        await partition.FlushAsync().ConfigureAwait(false);
                }
                else if (!skipCommitted)
                {
                    throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
                }
            }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ValueTask UnsafeAppendAsync<TEntry>(TEntry entry, long startIndex, [NotNull] out Partition? partition, CancellationToken token = default)
            where TEntry : notnull, IRaftLogEntry
        {
            partition = null;
            GetOrCreatePartition(startIndex, ref partition);
            return partition.WriteAsync(sessionManager.WriteSession, entry, startIndex, token);
        }

        private async ValueTask UnsafeAppendAsync<TEntry>(TEntry entry, long startIndex, bool flush)
            where TEntry : notnull, IRaftLogEntry
        {
            Debug.Assert(startIndex <= state.LastIndex + 1L);
            Debug.Assert(startIndex > state.CommitIndex);

            await UnsafeAppendAsync(entry, startIndex, out var partition).ConfigureAwait(false);
            state.LastIndex = startIndex;
            if (flush)
            {
                await partition.FlushAsync().ConfigureAwait(false);
                state.Flush();
            }

            writeCounter?.Invoke(1D);
        }

        private async ValueTask<long> UnsafeAppendAsync<TEntry>(TEntry entry, bool flush, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry
        {
            var startIndex = state.LastIndex + 1L;
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
            if (IsDisposed)
            {
                result = new(DisposedTask);
            }
            else if (startIndex <= state.CommitIndex)
            {
#if NETSTANDARD2_1
                result = new(Task.FromException(new InvalidOperationException(ExceptionMessages.InvalidAppendIndex)));
#else
                result = ValueTask.FromException(new InvalidOperationException(ExceptionMessages.InvalidAppendIndex));
#endif
            }
            else if (startIndex > state.LastIndex + 1L)
            {
#if NETSTANDARD2_1
                result = new(Task.FromException(new ArgumentOutOfRangeException(nameof(startIndex))));
#else
                result = ValueTask.FromException(new ArgumentOutOfRangeException(nameof(startIndex)));
#endif
            }
            else if (entry.IsSnapshot)
            {
#if NETSTANDARD2_1
                result = new (Task.FromException(new InvalidOperationException(ExceptionMessages.SnapshotDetected)));
#else
                result = ValueTask.FromException(new InvalidOperationException(ExceptionMessages.SnapshotDetected));
#endif
            }
            else if (Validate(in writeLock))
            {
                result = UnsafeAppendAsync(entry, startIndex, true);
            }
            else
            {
#if NETSTANDARD2_1
                result = new (Task.FromException(new ArgumentException(ExceptionMessages.InvalidLockToken, nameof(writeLock))));
#else
                result = ValueTask.FromException(new ArgumentException(ExceptionMessages.InvalidLockToken, nameof(writeLock)));
#endif
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
                    if (startIndex <= state.CommitIndex)
                        throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
                    if (startIndex > state.LastIndex + 1L)
                        throw new ArgumentOutOfRangeException(nameof(startIndex));

                    await UnsafeAppendAsync(entry, startIndex, true).ConfigureAwait(false);
                }
                finally
                {
                    syncRoot.ReleaseWriteLock();
                }
            }

            async ValueTask InstallSnapshotAsync(TEntry entry, long startIndex)
            {
                Debug.Assert(entry.IsSnapshot);
                Partition? removedHead;

                // Snapshot requires exclusive lock. However, snapshot installation is very rare operation
                await syncRoot.AcquireExclusiveLockAsync().ConfigureAwait(false);
                try
                {
                    if (startIndex <= state.CommitIndex)
                        throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
                    removedHead = await UnsafeInstallSnapshotAsync(entry, startIndex).ConfigureAwait(false);
                }
                finally
                {
                    syncRoot.ReleaseExclusiveLock();
                }

                await DeletePartitionsAsync(removedHead).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Adds uncommitted log entry to the end of this log.
        /// </summary>
        /// <remarks>
        /// This method cannot be used to append a snapshot.
        /// It's recommended to pass <see langword="true"/> to <paramref name="flush"/>
        /// only when you're adding the last log entry in the sequence.
        /// </remarks>
        /// <param name="writeLock">The acquired lock token.</param>
        /// <param name="entry">The entry to add.</param>
        /// <param name="flush"><see langword="true"/> to flush the internal buffer to the disk.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <typeparam name="TEntry">The actual type of the supplied log entry.</typeparam>
        /// <returns>The index of the added entry.</returns>
        /// <exception cref="ArgumentException"><paramref name="writeLock"/> is invalid.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="entry"/> is the snapshot entry.</exception>
        public ValueTask<long> AppendAsync<TEntry>(in WriteLockToken writeLock, TEntry entry, bool flush = true, CancellationToken token = default)
            where TEntry : notnull, IRaftLogEntry
        {
            ValueTask<long> result;
            if (IsDisposed)
            {
                result = new(GetDisposedTask<long>());
            }
            else if (entry.IsSnapshot)
            {
#if NETSTANDARD2_1
                result = new (Task.FromException<long>(new InvalidOperationException(ExceptionMessages.SnapshotDetected)));
#else
                result = ValueTask.FromException<long>(new InvalidOperationException(ExceptionMessages.SnapshotDetected));
#endif
            }
            else if (Validate(in writeLock))
            {
                result = UnsafeAppendAsync(entry, flush, token);
            }
            else
            {
#if NETSTANDARD2_1
                result = new (Task.FromException<long>(new ArgumentException(ExceptionMessages.InvalidLockToken, nameof(writeLock))));
#else
                result = ValueTask.FromException<long>(new ArgumentException(ExceptionMessages.InvalidLockToken, nameof(writeLock)));
#endif
            }

            return result;
        }

        private async ValueTask<long> AppendUncachedAsync<TEntry>(TEntry entry, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry
        {
            await syncRoot.AcquireWriteLockAsync(token).ConfigureAwait(false);
            long startIndex;
            try
            {
                startIndex = await UnsafeAppendAsync(entry, true, token).ConfigureAwait(false);
            }
            finally
            {
                syncRoot.ReleaseWriteLock();
            }

            return startIndex;
        }

        private async ValueTask<long> AppendCachedAsync<TEntry>(TEntry entry, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry
        {
            Debug.Assert(bufferManager.IsCachingEnabled);

            // copy log entry to the memory
            var writer = bufferManager.CreateBufferWriter(entry.Length ?? bufferSize);

            await entry.WriteToAsync(writer, token).ConfigureAwait(false);
            var cachedEntry = new CachedLogEntry(writer, entry.Term, entry.Timestamp, entry.CommandId);

            await syncRoot.AcquireWriteLockAsync(token).ConfigureAwait(false);
            long startIndex;
            try
            {
                // append it to the log
                startIndex = await UnsafeAppendAsync(cachedEntry, false, token).ConfigureAwait(false);
            }
            finally
            {
                syncRoot.ReleaseWriteLock();
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
            => AppendAsync(entry, false, token);

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
#if NETSTANDARD2_1
                result = new (Task.FromException<long>(new InvalidOperationException(ExceptionMessages.SnapshotDetected)));
#else
                result = ValueTask.FromException<long>(new InvalidOperationException(ExceptionMessages.SnapshotDetected));
#endif
            else if (addToCache && bufferManager.IsCachingEnabled)
                result = AppendCachedAsync(entry, token);
            else
                result = AppendUncachedAsync(entry, token);

            return result;
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
            ThrowIfDisposed();
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
            ThrowIfDisposed();
            var count = 0L;
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
                    await DropPartitionAsync(partition).ConfigureAwait(false);
                }
            }
            finally
            {
                syncRoot.ReleaseWriteLock();
            }

        exit:
            return count;

            ValueTask DropPartitionAsync(Partition partition)
            {
                if (ReferenceEquals(head, partition))
                    head = partition.Next;
                if (ReferenceEquals(tail, partition))
                    tail = partition.Previous;
                partition.Detach();
                return DeletePartitionAsync(partition);
            }
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

        private async ValueTask<Partition?> ForceCompactionAsync(long upperBoundIndex, SnapshotBuilder builder, CancellationToken token)
        {
            LogEntry entry;

            // 1. Initialize builder with snapshot record
            if (!snapshot.IsEmpty)
            {
                entry = await snapshot.ReadAsync(sessionManager.CompactionSession, token).ConfigureAwait(false);
                await builder.ApplyCoreAsync(entry).ConfigureAwait(false);
            }

            // 2. Do compaction
            Partition? current = head;
            Debug.Assert(current is not null);
            for (long startIndex = snapshot.Index + 1L, currentIndex = startIndex; TryGetPartition(builder, startIndex, upperBoundIndex, ref currentIndex, ref current) && current is not null && startIndex <= upperBoundIndex; currentIndex++, token.ThrowIfCancellationRequested())
            {
                entry = current.Read(sessionManager.CompactionSession, currentIndex, true);
                await builder.ApplyCoreAsync(entry).ConfigureAwait(false);
            }

            // update counter
            compactionCounter?.Invoke(upperBoundIndex - snapshot.Index);

            // 3. Persist snapshot (cannot be canceled to avoid inconsistency)
            await snapshot.WriteAsync(sessionManager.CompactionSession, builder, upperBoundIndex).ConfigureAwait(false);
            await snapshot.FlushAsync().ConfigureAwait(false);

            // 4. Remove squashed partitions
            return DetachPartitions(upperBoundIndex);

            bool TryGetPartition(SnapshotBuilder builder, long startIndex, long endIndex, ref long currentIndex, ref Partition? partition)
            {
                builder.AdjustIndex(startIndex, endIndex, ref currentIndex);
                return currentIndex.Between(startIndex, endIndex, BoundType.Closed) && this.TryGetPartition(currentIndex, ref partition);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsCompactionRequired(long upperBoundIndex)
            => upperBoundIndex - Volatile.Read(ref snapshot).Index >= recordsPerPartition;

        // In case of background compaction we need to have 1 fully committed partition as a divider
        // between partitions produced during writes and partitions to be compacted.
        // This restriction guarantees that compaction and writer thread will not be concurrent
        // when modifying Partition.next and Partition.previous fields need to keep sorted linked list
        // consistent and sorted.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long GetBackgroundCompactionCount(out long snapshotIndex)
        {
            snapshotIndex = Volatile.Read(ref snapshot).Index;
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
#if NETSTANDARD2_1
                result = new (Task.FromException(new ArgumentOutOfRangeException(nameof(count))));
#else
                result = ValueTask.FromException(new ArgumentOutOfRangeException(nameof(count)));
#endif
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
                var builder = CreateSnapshotBuilder();
                if (builder is not null)
                {
                    Partition? removedHead;
                    await syncRoot.AcquireCompactionLockAsync(token).ConfigureAwait(false);
                    try
                    {
                        // check compaction range again because snapshot index can be modified by snapshot installation method
                        var upperBoundIndex = ComputeUpperBoundIndex(count);
                        removedHead = IsCompactionRequired(upperBoundIndex) ?
                            await ForceCompactionAsync(upperBoundIndex, builder, token).ConfigureAwait(false) :
                            null;
                    }
                    finally
                    {
                        syncRoot.ReleaseCompactionLock();
                        builder.Dispose();
                    }

                    await DeletePartitionsAsync(removedHead).ConfigureAwait(false);
                }
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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            long FinalizeCommit(long count)
            {
                if (count > 0L)
                {
                    commitEvent.Set(true);
                }
                else
                {
                    count = 0L;
                }

                commitCounter?.Invoke(count);
                return count;
            }

            async ValueTask<long> CommitAndCompactSequentiallyAsync(long? endIndex, CancellationToken token)
            {
                Partition? removedHead;
                long count;
                await syncRoot.AcquireExclusiveLockAsync(token).ConfigureAwait(false);
                try
                {
                    count = GetCommitIndexAndCount(in endIndex, out var commitIndex);
                    if (count > 0)
                    {
                        state.CommitIndex = commitIndex;
                        await ApplyAsync(token).ConfigureAwait(false);
                        removedHead = await ForceSequentialCompactionAsync(commitIndex, token).ConfigureAwait(false);
                    }
                    else
                    {
                        removedHead = null;
                    }
                }
                finally
                {
                    syncRoot.ReleaseExclusiveLock();
                }

                await DeletePartitionsAsync(removedHead).ConfigureAwait(false);
                return FinalizeCommit(count);
            }

            async ValueTask<Partition?> ForceSequentialCompactionAsync(long upperBoundIndex, CancellationToken token)
            {
                SnapshotBuilder? builder;
                Partition? removedHead;
                if (IsCompactionRequired(upperBoundIndex) && (builder = CreateSnapshotBuilder()) is not null)
                {
                    using (builder)
                    {
                        removedHead = await ForceCompactionAsync(upperBoundIndex, builder, token).ConfigureAwait(false);
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
                await syncRoot.AcquireWriteLockAsync(token).ConfigureAwait(false);
                try
                {
                    count = GetCommitIndexAndCount(in endIndex, out var commitIndex);
                    if (count > 0)
                    {
                        state.CommitIndex = commitIndex;
                        await ApplyAsync(token).ConfigureAwait(false);
                    }
                }
                finally
                {
                    syncRoot.ReleaseWriteLock();
                }

                return FinalizeCommit(count);
            }

            async Task<Partition?> ForceIncrementalCompactionAsync(long upperBoundIndex, CancellationToken token)
            {
                Partition? removedHead;
                SnapshotBuilder? builder;
                if (upperBoundIndex > 0L && (builder = CreateSnapshotBuilder()) is not null)
                {
                    await Task.Yield();
                    using (builder)
                    {
                        removedHead = await ForceCompactionAsync(upperBoundIndex, builder, token).ConfigureAwait(false);
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
                await syncRoot.AcquireExclusiveLockAsync(token).ConfigureAwait(false);
                try
                {
                    count = GetCommitIndexAndCount(in endIndex, out var commitIndex);
                    if (count > 0)
                    {
                        var compactionIndex = Math.Min(state.CommitIndex, snapshot.Index + count);
                        state.CommitIndex = commitIndex;
                        var compaction = ForceIncrementalCompactionAsync(compactionIndex, token);
                        try
                        {
                            await ApplyAsync(token).ConfigureAwait(false);
                        }
                        finally
                        {
                            removedHead = await compaction.ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        removedHead = null;
                    }
                }
                finally
                {
                    syncRoot.ReleaseExclusiveLock();
                }

                await DeletePartitionsAsync(removedHead).ConfigureAwait(false);
                return FinalizeCommit(count);
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
        protected virtual ValueTask ApplyAsync(LogEntry entry) => new();

        /// <summary>
        /// Flushes the underlying data storage.
        /// </summary>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        protected virtual ValueTask FlushAsync() => new();

        private async ValueTask ApplyAsync(long startIndex, CancellationToken token)
        {
            var commitIndex = state.CommitIndex;
            for (Partition? partition = null; startIndex <= commitIndex; state.LastApplied = startIndex++, token.ThrowIfCancellationRequested())
            {
                if (TryGetPartition(startIndex, ref partition))
                {
                    var entry = partition.Read(sessionManager.WriteSession, startIndex, true);
                    await ApplyAsync(entry).ConfigureAwait(false);
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
            ThrowIfDisposed();
            await syncRoot.AcquireExclusiveLockAsync(token).ConfigureAwait(false);
            try
            {
                LogEntry entry;
                long startIndex;

                // 1. Apply snapshot if it not empty
                if (!snapshot.IsEmpty)
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
            if (token.IsCancellationRequested)
                return Task.FromCanceled(token);

            return replayOnInitialize ? ReplayAsync(token) : Task.CompletedTask;
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
        async ValueTask<long> IPersistentState.IncrementTermAsync()
        {
            long result;
            await syncRoot.AcquireWriteLockAsync().ConfigureAwait(false);
            try
            {
                result = state.IncrementTerm();
            }
            finally
            {
                syncRoot.ReleaseWriteLock();
            }

            return result;
        }

        /// <inheritdoc/>
        async ValueTask IPersistentState.UpdateTermAsync(long term, bool resetLastVote)
        {
            await syncRoot.AcquireWriteLockAsync().ConfigureAwait(false);
            try
            {
                state.UpdateTerm(term, resetLastVote);
            }
            finally
            {
                syncRoot.ReleaseWriteLock();
            }
        }

        /// <inheritdoc/>
        async ValueTask IPersistentState.UpdateVotedForAsync(IRaftClusterMember? member)
        {
            await syncRoot.AcquireWriteLockAsync().ConfigureAwait(false);
            try
            {
                state.UpdateVotedFor(member?.Id);
            }
            finally
            {
                syncRoot.ReleaseWriteLock();
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
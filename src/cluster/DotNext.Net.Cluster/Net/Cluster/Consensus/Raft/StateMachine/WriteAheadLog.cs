using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using static System.Threading.Timeout;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Buffers;
using IO;
using IO.Log;
using Threading;
using Threading.Tasks;

/// <summary>
/// Represents the general-purpose Raft WAL.
/// </summary>
[Experimental("DOTNEXT001")]
public partial class WriteAheadLog : Disposable, IAsyncDisposable, IPersistentState
{
    private const int DictionaryConcurrencyLevel = 3; // append flow and cleaner and applier

    private readonly MemoryAllocator<byte> bufferAllocator;
    private readonly IStateMachine stateMachine;
    private readonly CancellationToken lifetimeToken;
    
    private volatile ExceptionDispatchInfo? backgroundTaskFailure;
    private long lastEntryIndex; // Append lock protects modification of this field
    
    // lifetime management
    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private CancellationTokenSource? lifetimeTokenSource;

    /// <summary>
    /// Initializes a new WAL.
    /// </summary>
    /// <param name="configuration">The configuration of the write-ahead log.</param>
    /// <param name="stateMachine">The state machine.</param>
    public WriteAheadLog(Options configuration, IStateMachine stateMachine)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(stateMachine);

        if (nuint.Size < sizeof(ulong))
            throw new PlatformNotSupportedException();

        lifetimeToken = (lifetimeTokenSource = new()).Token;
        var rootPath = new DirectoryInfo(configuration.Location);
        rootPath.CreateIfNeeded();

        context = new(DictionaryConcurrencyLevel, configuration.ConcurrencyLevel);
        lockManager = new(configuration.ConcurrencyLevel) { MeasurementTags = configuration.MeasurementTags };
        bufferAllocator = configuration.Allocator ?? ArrayPool<byte>.Shared.ToAllocator();
        this.stateMachine = stateMachine;
        stateLock = new(configuration.ConcurrencyLevel) { MeasurementTags = configuration.MeasurementTags };
        state = new(rootPath);
        measurementTags = configuration.MeasurementTags;

        checkpoint = new(rootPath, out var lastReliablyWrittenEntryIndex);
        
        // page management
        {
            var metadataLocation = rootPath.GetSubdirectory(MetadataPageManager.LocationPrefix);
            metadataLocation.CreateIfNeeded();

            var dataLocation = rootPath.GetSubdirectory(PagedBufferWriter.LocationPrefix);
            dataLocation.CreateIfNeeded();

            PageManager m, d;
            switch (configuration.MemoryManagement)
            {
                case MemoryManagementStrategy.PrivateMemory:
                    m = new AnonymousPageManager(metadataLocation, Page.MinSize);
                    d = new AnonymousPageManager(dataLocation, configuration.ChunkMaxSize);
                    break;
                case MemoryManagementStrategy.SharedMemory:
                default:
                    m = new MemoryMappedPageManager(metadataLocation, Page.MinSize);
                    d = new MemoryMappedPageManager(dataLocation, configuration.ChunkMaxSize);
                    break;
            }

            metadataPages = new(m);
            dataPages = new(d)
            {
                LastWrittenAddress = metadataPages.TryGetMetadata(lastReliablyWrittenEntryIndex, out var metadata)
                    ? metadata.End
                    : 0UL,
            };
        }

        var snapshotIndex = stateMachine.Snapshot?.Index ?? 0L;
        LastEntryIndex = LastCommittedEntryIndex = long.Max(lastReliablyWrittenEntryIndex, snapshotIndex);
        
        // flusher
        {
            var interval = configuration.FlushInterval;
            flusherPreviousIndex = commitIndex + 1L;
            if (interval == TimeSpan.Zero)
            {
                flushTrigger = new(initialState: false);
                flusherTask = FlushAsync(new BackgroundTrigger(flushTrigger, out flushCompleted), lifetimeToken);
            }
            else if (interval == InfiniteTimeSpan)
            {
                flusherTask = Task.CompletedTask;
            }
            else
            {
                flusherTask = FlushAsync(new TimeoutTrigger(interval, out flushCompleted), lifetimeToken);
            }
        }

        // applier
        {
            appliedIndex = snapshotIndex;
            applyTrigger = new();
            appenderTask = ApplyAsync(lifetimeTokenSource.Token);
            appliedEvent = new(configuration.ConcurrencyLevel) { MeasurementTags = configuration.MeasurementTags };
        }
    }

    private long SnapshotIndex => stateMachine.Snapshot?.Index ?? 0L;

    /// <summary>
    /// Initializes the log asynchronously.
    /// </summary>
    /// <remarks>
    /// The default implementation applies committed log entries to the underlying state machine.
    /// </remarks>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns></returns>
    public virtual Task InitializeAsync(CancellationToken token = default)
        => WaitForApplyAsync(LastCommittedEntryIndex, token).AsTask();

    /// <inheritdoc cref="IAuditTrail.LastEntryIndex"/>
    public long LastEntryIndex
    {
        get => Volatile.Read(ref lastEntryIndex);
        private set => Volatile.Write(ref lastEntryIndex, value);
    }

    private async ValueTask<long> AppendUnbufferedAsync<TEntry>(TEntry entry, CancellationToken token)
        where TEntry : IRaftLogEntry
    {
        long currentIndex;
        lockManager.SetCallerInformation("Append Single Entry");
        await lockManager.AcquireAppendLockAsync(token).ConfigureAwait(false);
        try
        {
            currentIndex = LastEntryIndex + 1L;

            await AppendAsync(entry, out var startAddress, token).ConfigureAwait(false);
            WriteMetadata(entry, currentIndex, startAddress);
        }
        finally
        {
            lockManager.ReleaseAppendLock();
        }

        return currentIndex;
    }
    
    private async ValueTask<long> AppendBufferedAsync<TEntry>(TEntry entry, CancellationToken token)
        where TEntry : struct, IBufferedLogEntry
    {
        lockManager.SetCallerInformation("Append Single Buffered Entry");
        await lockManager.AcquireAppendLockAsync(token).ConfigureAwait(false);
        try
        {
            return AppendBuffered(entry);
        }
        finally
        {
            lockManager.ReleaseAppendLock();
            if (typeof(TEntry) == typeof(BufferedLogEntry))
                Unsafe.As<TEntry, BufferedLogEntry>(ref entry).Dispose();
        }
    }

    private long AppendBuffered<TEntry>(TEntry entry)
        where TEntry : struct, IBufferedLogEntry
    {
        var currentIndex = LastEntryIndex + 1L;
        var startAddress = dataPages.LastWrittenAddress;
        dataPages.Write(entry.Content);
        WriteMetadata(entry, currentIndex, startAddress);
        return currentIndex;
    }

    /// <inheritdoc cref="IAuditTrail{TEntryImpl}.AppendAsync{TEntry}(TEntry, CancellationToken)"/>
    public ValueTask<long> AppendAsync<TEntry>(TEntry entry, CancellationToken token = default)
        where TEntry : IRaftLogEntry
    {
        ValueTask<long> task;
        if (IsDisposingOrDisposed)
        {
            task = new(GetDisposedTask<long>());
        }
        else if (backgroundTaskFailure?.SourceException is { } exception)
        {
            task = ValueTask.FromException<long>(exception);
        }
        else if (typeof(TEntry) == typeof(BinaryLogEntry))
        {
            task = AppendBufferedAsync(Unsafe.As<TEntry, BinaryLogEntry>(ref entry), token);
        }
        else if (entry.IsSnapshot)
        {
            task = ValueTask.FromException<long>(new InvalidOperationException(ExceptionMessages.SnapshotDetected));
        }
        else if (entry.TryGetMemory(out var payload))
        {
            var entryCopy = new BinaryLogEntry
            {
                Term = entry.Term,
                Timestamp = entry.Timestamp,
                Content = payload,
                CommandId = entry.CommandId,
                Context = entry is IInputLogEntry { Context: { } ctx } ? ctx : null,
            };

            task = AppendBufferedAsync(entryCopy, token);
        }
        else if (entry is ISupplier<MemoryAllocator<byte>, MemoryOwner<byte>>)
        {
            // make a copy out of the lock
            var entryCopy = new BufferedLogEntry(((ISupplier<MemoryAllocator<byte>, MemoryOwner<byte>>)entry).Invoke(bufferAllocator))
            {
                Term = entry.Term,
                Timestamp = entry.Timestamp,
                CommandId = entry.CommandId,
                Context = entry is IInputLogEntry { Context: { } ctx } ? ctx : null,
            };

            task = AppendBufferedAsync(entryCopy, token);
        }
        else
        {
            task = AppendUnbufferedAsync(entry, token);
        }

        return task;
    }

    /// <inheritdoc cref="IAuditTrail{TEntryImpl}.AppendAsync{TEntry}(TEntry, long, CancellationToken)"/>
    public async ValueTask AppendAsync<TEntry>(TEntry entry, long startIndex, CancellationToken token = default)
        where TEntry : IRaftLogEntry
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        ObjectDisposedException.ThrowIf(IsDisposingOrDisposed, this);
        backgroundTaskFailure?.Throw();

        lockManager.SetCallerInformation("Append Single Entry at Custom Index");
        await lockManager.AcquireAppendLockAsync(token).ConfigureAwait(false);
        try
        {
            var tailIndex = LastEntryIndex;
            if (entry.IsSnapshot)
            {
                await lockManager.UpgradeToOverwriteLockAsync(token).ConfigureAwait(false);
                if (startIndex <= LastCommittedEntryIndex)
                    throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
                
                LastAppliedIndex = await stateMachine.ApplyAsync(new LogEntry(entry, startIndex), token).ConfigureAwait(false);
                var snapshotIndex = stateMachine.Snapshot?.Index ?? startIndex;
                LastEntryIndex = long.Max(tailIndex, LastCommittedEntryIndex = snapshotIndex);
            }
            else
            {
                switch (startIndex.CompareTo(++tailIndex))
                {
                    case > 0:
                        throw new ArgumentOutOfRangeException(nameof(startIndex));
                    case < 0:
                        await lockManager.UpgradeToOverwriteLockAsync(token).ConfigureAwait(false);
                        if (startIndex <= LastCommittedEntryIndex)
                            throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
                        break;
                }

                await AppendAsync(entry, out var startAddress, token).ConfigureAwait(false);
                WriteMetadata(entry, startIndex, startAddress);
            }
        }
        finally
        {
            lockManager.ReleaseAppendLock();
        }
    }

    /// <inheritdoc cref="IAuditTrail{TEntryImpl}.AppendAsync{TEntry}(ILogEntryProducer{TEntry}, long, bool, CancellationToken)"/>
    public async ValueTask AppendAsync<TEntry>(ILogEntryProducer<TEntry> entries, long startIndex, bool skipCommitted = false,
        CancellationToken token = default)
        where TEntry : IRaftLogEntry
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        ObjectDisposedException.ThrowIf(IsDisposingOrDisposed, this);
        backgroundTaskFailure?.Throw();

        lockManager.SetCallerInformation("Append Multiple Entries");
        await lockManager.AcquireAppendLockAsync(token).ConfigureAwait(false);
        try
        {
            switch (startIndex.CompareTo(LastEntryIndex + 1L))
            {
                case > 0:
                    throw new ArgumentOutOfRangeException(nameof(startIndex));
                case < 0:
                    await lockManager.UpgradeToOverwriteLockAsync(token).ConfigureAwait(false);
                    break;
            }
            
            await AppendCoreAsync(entries, startIndex, skipCommitted, token).ConfigureAwait(false);
        }
        finally
        {
            lockManager.ReleaseAppendLock();
        }
    }

    private async ValueTask AppendCoreAsync<TEntry>(ILogEntryProducer<TEntry> entries, long startIndex, bool skipCommitted, CancellationToken token)
        where TEntry : IRaftLogEntry
    {
        for (var commitIndex = LastCommittedEntryIndex; await entries.MoveNextAsync().ConfigureAwait(false); startIndex++)
        {
            if (entries.Current is not { IsSnapshot: false } currentEntry)
                throw new InvalidOperationException(ExceptionMessages.SnapshotDetected);

            if (startIndex > commitIndex)
            {
                await AppendAsync(currentEntry, out var startAddress, token).ConfigureAwait(false);
                WriteMetadata(currentEntry, startIndex, startAddress);
            }
            else if (!skipCommitted)
            {
                throw new InvalidOperationException(ExceptionMessages.InvalidAppendIndex);
            }
        }
    }

    /// <inheritdoc/>
    ValueTask<long> IAuditTrail<IRaftLogEntry>.AppendAndCommitAsync<TEntry>(ILogEntryProducer<TEntry> entries, long startIndex, bool skipCommitted,
        long commitIndex, CancellationToken token)
    {
        return IsDisposingOrDisposed
            ? new(GetDisposedTask<long>())
            : backgroundTaskFailure?.SourceException is { } exception
                ? ValueTask.FromException<long>(exception)
                : entries.RemainingCount is 0L
                    ? CommitAsync(commitIndex, token)
                    : commitIndex < startIndex
                        ? AppendAndCommitAsync(entries, startIndex, skipCommitted, commitIndex, token)
                        : AppendAndCommitSlowAsync(entries, startIndex, skipCommitted, commitIndex, token);
    }

    private async ValueTask<long> AppendAndCommitAsync<TEntry>(ILogEntryProducer<TEntry> entries, long startIndex, bool skipCommitted,
        long commitIndex, CancellationToken token)
        where TEntry : IRaftLogEntry
    {
        // the best case for this method - raise flusher and applier in parallel with the appending process
        var committedCount = await CommitCoreAsync<FalseConstant>(commitIndex, token).ConfigureAwait(false);
        bool delayedPostCommit;

        lockManager.SetCallerInformation("Append and Commit");
        await lockManager.AcquireAppendLockAsync(token).ConfigureAwait(false);
        try
        {
            switch (startIndex.CompareTo(LastEntryIndex + 1L))
            {
                case > 0:
                    throw new ArgumentOutOfRangeException(nameof(startIndex));
                case < 0:
                    // No need to call PostCommit here since the o/w lock will suspend the flusher and applier.
                    // Thus, we can resume them later, out of the o/w lock
                    await lockManager.UpgradeToOverwriteLockAsync(token).ConfigureAwait(false);
                    delayedPostCommit = committedCount > 0L;
                    break;
                case 0 when committedCount > 0L:
                    OnCommitted(committedCount);
                    goto default;
                default:
                    delayedPostCommit = false;
                    break;
            }

            await AppendCoreAsync(entries, startIndex, skipCommitted, token).ConfigureAwait(false);
        }
        finally
        {
            lockManager.ReleaseAppendLock();
        }

        if (delayedPostCommit)
        {
            OnCommitted(committedCount);
        }

        return committedCount;
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<long> AppendAndCommitSlowAsync<TEntry>(ILogEntryProducer<TEntry> entries, long startIndex, bool skipCommitted,
        long commitIndex, CancellationToken token)
        where TEntry : IRaftLogEntry
    {
        await AppendAsync(entries, startIndex, skipCommitted, token).ConfigureAwait(false);
        return await CommitAsync(commitIndex, token).ConfigureAwait(false);
    }

    private ValueTask AppendAsync<TEntry>(TEntry entry, out ulong startAddress, CancellationToken token)
        where TEntry : IRaftLogEntry
    {
        startAddress = dataPages.LastWrittenAddress;

        ValueTask task;
        if (entry.TryGetMemory(out var memory))
        {
            task = ValueTask.CompletedTask;
            try
            {
                dataPages.Write(memory.Span);
            }
            catch (Exception e)
            {
                task = ValueTask.FromException(e);
            }
        }
        else if (dataPages.TryEnsureCapacity(entry.Length))
        {
            task = entry.WriteToAsync(dataPages, token);
        }
        else
        {
            task = WriteSlowAsync(dataPages, entry, bufferAllocator, token);
        }

        return task;

        static async ValueTask WriteSlowAsync(IBufferWriter<byte> writer, TEntry entry, MemoryAllocator<byte> allocator, CancellationToken token)
        {
            const int bufferSize = 1024;
            var buffer = allocator.AllocateAtLeast(bufferSize);
            var stream = writer.AsStream();
            try
            {
                await entry.WriteToAsync(stream, buffer.Memory, token).ConfigureAwait(false);
            }
            finally
            {
                buffer.Dispose();
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private void WriteMetadata<TEntry>(TEntry entry, long index, ulong startAddress)
        where TEntry : IRaftLogEntry
    {
        var length = long.CreateChecked(dataPages.LastWrittenAddress - startAddress);
        metadataPages[index] = LogEntryMetadata.Create(entry, startAddress, length);

        if (entry is IInputLogEntry { Context: { } ctx })
        {
            context[index] = ctx;
        }

        LastEntryIndex = index;
        AppendRateMeter.Add(1L, measurementTags);
        BytesWrittenMeter.Record(length, measurementTags);
    }

    /// <inheritdoc cref="IAuditTrail.CommitAsync(long, CancellationToken)"/>
    public ValueTask<long> CommitAsync(long endIndex, CancellationToken token = default)
    {
        ValueTask<long> task;
        if (endIndex < 0L || endIndex > LastEntryIndex)
        {
            task = ValueTask.FromException<long>(new ArgumentOutOfRangeException(nameof(endIndex)));
        }
        else if (IsDisposingOrDisposed)
        {
            task = new(GetDisposedTask<long>());
        }
        else if (backgroundTaskFailure?.SourceException is { } exception)
        {
            task = ValueTask.FromException<long>(exception);
        }
        else
        {
            task = CommitCoreAsync<TrueConstant>(endIndex, token);
        }

        return task;
    }

    private ValueTask<long> CommitCoreAsync<TNotify>(long endIndex, CancellationToken token)
        where TNotify : struct, IConstant<bool>
    {
        ValueTask<long> task;

        if (lockManager.TryAcquireCommitLock())
        {
            var count = Commit(endIndex);
            lockManager.ReleaseCommitLock();
            
            // notify out of the lock
            try
            {
                if (TNotify.Value && count > 0L)
                    OnCommitted(count);

                task = new(count);
            }
            catch (Exception e)
            {
                task = ValueTask.FromException<long>(e);
            }
        }
        else
        {
            task = CommitSlowAsync<TNotify>(endIndex, token);
        }

        return task;
    }

    private async ValueTask<long> CommitSlowAsync<TNotify>(long endIndex, CancellationToken token)
        where TNotify : struct, IConstant<bool>
    {
        lockManager.SetCallerInformation("Commit");
        await lockManager.AcquireCommitLockAsync(token).ConfigureAwait(false);
        var count = Commit(endIndex);
        lockManager.ReleaseCommitLock();

        if (TNotify.Value && count > 0L)
            OnCommitted(count);

        return count;
    }

    /// <inheritdoc cref="IAuditTrail.WaitForApplyAsync(CancellationToken)"/>
    public ValueTask WaitForApplyAsync(CancellationToken token = default)
        => backgroundTaskFailure?.SourceException is { } exception
            ? ValueTask.FromException(exception)
            : appliedEvent.WaitAsync(token);

    /// <inheritdoc cref="IAuditTrail.WaitForApplyAsync(long, CancellationToken)"/>
    public ValueTask WaitForApplyAsync(long index, CancellationToken token = default)
        => backgroundTaskFailure?.SourceException is { } exception
            ? ValueTask.FromException(exception)
            : appliedEvent.SpinWaitAsync<CommitChecker>(new(this, index), token);

    /// <inheritdoc cref="IAuditTrail{TEntryImpl}.ReadAsync{TResult}(ILogEntryConsumer{TEntryImpl, TResult}, long, long, CancellationToken)"/>
    public ValueTask<TResult> ReadAsync<TResult>(ILogEntryConsumer<IRaftLogEntry, TResult> reader, long startIndex, long endIndex,
        CancellationToken token = default)
    {
        ValueTask<TResult> task;
        if (IsDisposingOrDisposed)
            task = new(GetDisposedTask<TResult>());
        else if (startIndex < 0L)
            task = ValueTask.FromException<TResult>(new ArgumentOutOfRangeException(nameof(startIndex)));
        else if (endIndex < 0L || endIndex > LastEntryIndex)
            task = ValueTask.FromException<TResult>(new ArgumentOutOfRangeException(nameof(endIndex)));
        else if (backgroundTaskFailure?.SourceException is { } exception)
            task = ValueTask.FromException<TResult>(exception);
        else if (startIndex > endIndex)
            task = reader.ReadAsync<LogEntry, LogEntry[]>([], null, token);
        else
            task = ReadCoreAsync(reader, startIndex, endIndex, token);

        return task;
    }

    private async ValueTask<TResult> ReadCoreAsync<TResult>(ILogEntryConsumer<IRaftLogEntry, TResult> reader, long startIndex, long endIndex,
        CancellationToken token = default)
    {
        lockManager.SetCallerInformation("Read Entries");
        await lockManager.AcquireReadLockAsync(token).ConfigureAwait(false);
        try
        {
            var list = new LogEntryList(
                stateMachine,
                startIndex,
                endIndex,
                reader.LogEntryMetadataOnly ? null : dataPages,
                metadataPages,
                out var snapshotIndex);
            return await reader.ReadAsync<LogEntry, LogEntryList>(list, snapshotIndex, token).ConfigureAwait(false);
        }
        finally
        {
            lockManager.ReleaseReadLock();
        }
    }

    private void CleanUp()
    {
        metadataPages.Dispose();
        dataPages.Dispose();
        Dispose<QueuedSynchronizer>([lockManager, appliedEvent, stateLock]);
        checkpoint.Dispose();
        state.Dispose();
        context.Clear();
        backgroundTaskFailure = null;
    }

    private void CancelBackgroundJobs()
    {
        if (Interlocked.Exchange(ref lifetimeTokenSource, null) is { } cts)
        {
            using (cts)
            {
                cts.Cancel();
            }
        }
        
        flushTrigger?.Set();
        applyTrigger.Set();
    }

    /// <inheritdoc/>
    protected override async ValueTask DisposeAsyncCore()
    {
        CancelBackgroundJobs();
        
        await flusherTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        await appenderTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        if (cleanupTask.TryGetTarget(out var task))
            await task.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        
        CleanUp();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeAsyncCore().Wait();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc cref="IAsyncDisposable.DisposeAsync()"/>
    public new ValueTask DisposeAsync() => base.DisposeAsync();
}

file static class DirectoryInfoExtensions
{
    public static void CreateIfNeeded(this DirectoryInfo directory)
    {
        if (!directory.Exists)
            directory.Create();
    }

    public static DirectoryInfo GetSubdirectory(this DirectoryInfo root, string prefix)
        => new(Path.Combine(root.FullName, prefix));
}
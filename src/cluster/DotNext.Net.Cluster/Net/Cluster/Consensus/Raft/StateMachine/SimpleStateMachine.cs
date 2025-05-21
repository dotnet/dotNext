using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using DotNext.IO;
using DotNext.Runtime.CompilerServices;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Commands;

/// <summary>
/// Represents a state machine that keeps the entire state in the memory but periodically
/// creates a persistent snapshot for recovery.
/// </summary>
[Experimental("DOTNEXT001")]
public abstract partial class SimpleStateMachine : IAsyncDisposable, IStateMachine
{
    private readonly CancellationToken lifetimeToken;
    private readonly DirectoryInfo location;
    private readonly Task<SnapshotWriter> sentinel;

    [SuppressMessage("Usage", "CA2213", Justification = "See DisposeAsync() implementation")]
    private CancellationTokenSource? lifetimeSource;
    private long appliedIndex;
    private volatile Snapshot? snapshot;
    private volatile Task<SnapshotWriter>? snapshottingProcess;

    /// <summary>
    /// Initializes a new simple state machine.
    /// </summary>
    /// <param name="location"></param>
    protected SimpleStateMachine(DirectoryInfo location)
    {
        if (!location.Exists)
            location.Create();

        this.location = location;
        lifetimeToken = (lifetimeSource = new()).Token;
        sentinel = Task.FromException<SnapshotWriter>(new ObjectDisposedException(GetType().Name));
    }

    /// <summary>
    /// Restores the in-memory state from the snapshot.
    /// </summary>
    /// <param name="snapshotFile">The snapshot file.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of the method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    protected abstract ValueTask RestoreAsync(FileInfo snapshotFile, CancellationToken token);

    /// <summary>
    /// Restores the in-memory state from the snapshot.
    /// </summary>
    /// <remarks>
    /// This method is intended to be called from <see cref="RestoreAsync(System.IO.FileInfo,System.Threading.CancellationToken)"/>
    /// when the compatibility with <see cref="CommandInterpreter"/> is required.
    /// </remarks>
    /// <param name="interpreter">The command interpreter.</param>
    /// <param name="snapshotFile">The snapshot file.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of the method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    protected static async ValueTask RestoreAsync(CommandInterpreter interpreter, FileInfo snapshotFile, CancellationToken token)
        => await interpreter.InterpretAsync(new Snapshot(snapshotFile), token).ConfigureAwait(false);
    
    /// <summary>
    /// Persists the current state.
    /// </summary>
    /// <param name="writer">The writer that can be used to write the state.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of the method.</returns>
    protected abstract ValueTask PersistAsync(IAsyncBinaryWriter writer, CancellationToken token);

    /// <summary>
    /// Restores the in-memory state from the most recent snapshot stored on the disk.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of the method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask RestoreAsync(CancellationToken token = default)
    {
        // find the most recent snapshot
        foreach (var candidate in GetSnapshots(location))
        {
            if (snapshot is null || candidate.Index > snapshot.Index)
            {
                snapshot = candidate;
                appliedIndex = candidate.Index;
            }
        }

        return snapshot is { File: { } snapshotFile }
            ? RestoreAsync(snapshotFile, token)
            : ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    ISnapshot? ISnapshotManager.Snapshot => snapshot;

    /// <inheritdoc/>
    ValueTask ISnapshotManager.ReclaimGarbageAsync(long watermark, CancellationToken token)
    {
        var task = ValueTask.CompletedTask;
        try
        {
            foreach (var candidate in GetSnapshots(location))
            {
                token.ThrowIfCancellationRequested();
                if (candidate.Index < watermark)
                    candidate.File.Delete();
            }
        }
        catch (OperationCanceledException e)
        {
            task = ValueTask.FromCanceled(e.CancellationToken);
        }
        catch (Exception e)
        {
            task = ValueTask.FromException(e);
        }

        return task;
    }

    /// <inheritdoc/>
    ValueTask<long> IStateMachine.ApplyAsync(LogEntry entry, CancellationToken token)
    {
        return appliedIndex >= entry.Index
            ? ValueTask.FromResult(appliedIndex)
            : entry.IsSnapshot
                ? InstallSnapshotAsync(entry, token)
                : ApplyCoreAsync(entry, token);
    }

    private async ValueTask<long> ApplyCoreAsync(LogEntry entry, CancellationToken token)
    {
        await EndSnapshottingAsync(commit: true).ConfigureAwait(false);

        if (await ApplyAsync(entry, token).ConfigureAwait(false))
        {
            var newTask = BeginSnapshottingAsync(entry.Index, entry.Term, lifetimeToken);

            if (Interlocked.CompareExchange(ref snapshottingProcess, newTask, null) is not null)
            {
                await newTask.ConfigureAwait(false);
            }
        }

        return appliedIndex = entry.Index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task EndSnapshottingAsync([ConstantExpected] bool commit)
        => snapshottingProcess is not { } task
            ? Task.CompletedTask
            : commit
                ? InstallSnapshotAsync(task)
                : RollbackSnapshotAsync(task);

    private async Task InstallSnapshotAsync(Task<SnapshotWriter> task)
    {
        var writer = await task.ConfigureAwait(false);
        writer.Dispose();

        if (ReferenceEquals(Interlocked.CompareExchange(ref snapshottingProcess, null, task), task))
        {
            writer.Commit();
            snapshot = new Snapshot(writer.Destination);
        }
    }

    private async Task RollbackSnapshotAsync(Task<SnapshotWriter> task)
    {
        var writer = await task.ConfigureAwait(false);
        writer.Dispose();
        writer.Rollback();
        Interlocked.CompareExchange(ref snapshottingProcess, null, task);
    }

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder<>))]
    private async Task<SnapshotWriter> BeginSnapshottingAsync(long index, long term, CancellationToken token)
    {
        var writer = new SnapshotWriter(preallocationSize: 0L, DateTime.UtcNow, Snapshot.CreateSnapshotFile(location, index, term));
        try
        {
            await PersistAsync(writer, token).ConfigureAwait(false);
            await writer.WriteAsync(token).ConfigureAwait(false);
            writer.FlushToDisk();
        }
        catch
        {
            writer.Dispose();
            writer.Rollback();
            throw;
        }

        return writer;
    }

    /// <summary>
    /// Applies the log entry to this state machine.
    /// </summary>
    /// <param name="entry">The log entry to apply to the current state machine.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if the current state must be persisted; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    protected abstract ValueTask<bool> ApplyAsync(LogEntry entry, CancellationToken token);

    private async ValueTask<long> InstallSnapshotAsync(LogEntry entry, CancellationToken token)
    {
        await EndSnapshottingAsync(commit: false).ConfigureAwait(false);
        
        var newSnapshot = new Snapshot(location, entry.Index, entry.Term);
        await newSnapshot.ReadFromAsync(entry, token).ConfigureAwait(false);
        await RestoreAsync(newSnapshot.File, token).ConfigureAwait(false);
        
        snapshot = newSnapshot;
        return appliedIndex = entry.Index;
    }

    private async ValueTask DisposeImplAsync(CancellationTokenSource cts)
    {
        using (cts)
        {
            cts.Cancel();
        }

        var task = Interlocked.Exchange(ref snapshottingProcess, sentinel);
        if (task is not null && !ReferenceEquals(task, sentinel))
        {
            await task.As<Task>().ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    /// <inheritdoc/>
    public virtual ValueTask DisposeAsync()
        => Interlocked.Exchange(ref lifetimeSource, null) is { } cts ? DisposeImplAsync(cts) : ValueTask.CompletedTask;
}
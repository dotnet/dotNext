using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Diagnostics;
using IO.Log;
using Runtime.CompilerServices;
using Threading;

partial class WriteAheadLog
{
    private readonly AsyncAutoResetEventSlim? flushTrigger;
    private readonly Task flusherTask;
    private readonly WeakReference<Task?> cleanupTask = new(target: null, trackResurrection: false);
    
    private Checkpoint checkpoint;
    private long commitIndex; // Commit lock protects modification of this field
    private long flusherPreviousIndex, flusherOldSnapshotIndex;

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task FlushAsync<T>(T flushTrigger, CancellationToken token)
        where T : struct, IFlushTrigger
    {
        // Weak ref tracks the task, but allows GC to collect associated state machine
        // as soon as possible. While the task is running, it cannot be collected, because it's referenced
        // by the async state machine.
        try
        {
            flusherOldSnapshotIndex = SnapshotIndex;
            for (long newIndex, newSnapshot;
                 !token.IsCancellationRequested && backgroundTaskFailure is null;
                 flusherPreviousIndex = long.Max(flusherOldSnapshotIndex = newSnapshot, newIndex) + 1L)
            {
                newSnapshot = SnapshotIndex;
                newIndex = LastCommittedEntryIndex;

                if (newIndex >= flusherPreviousIndex)
                {
                    // Ensure that the flusher is not running with the snapshot installation process concurrently
                    lockManager.SetCallerInformation("Flush Pages");
                    await lockManager.AcquireReadLockAsync(token).ConfigureAwait(false);
                    try
                    {
                        await Flush(flusherPreviousIndex, newIndex, token).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        backgroundTaskFailure = ExceptionDispatchInfo.Capture(e);
                        appliedEvent.Interrupt(e);
                        break;
                    }
                    finally
                    {
                        lockManager.ReleaseReadLock();
                    }
                }

                if ((!cleanupTask.TryGetTarget(out var task) || task.IsCompletedSuccessfully) && flusherOldSnapshotIndex < newSnapshot)
                    cleanupTask.SetTarget(CleanUpAsync(newSnapshot, token));

                await flushTrigger.WaitAsync(token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException e) when (e.CancellationToken == token && cleanupTask.TryGetTarget(out var task))
        {
            await task.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
        finally
        {
            cleanupTask.SetTarget(null);
            
            if (flushTrigger is IDisposable)
                ((IDisposable)flushTrigger).Dispose();
        }
    }

    private async ValueTask Flush(long fromIndex, long toIndex, CancellationToken token)
    {
        var ts = new Timestamp();
        await metadataPages.FlushAsync(fromIndex, toIndex, token).ConfigureAwait(false);

        var toMetadata = metadataPages[toIndex];
        var fromMetadata = metadataPages[fromIndex];
        await dataPages.FlushAsync(fromMetadata.Offset, toMetadata.End, token).ConfigureAwait(false);
        
        FlushDurationMeter.Record(ts.ElapsedMilliseconds);

        // everything up to toIndex is flushed, save the commit index
        await checkpoint.UpdateAsync(toIndex, token).ConfigureAwait(false);

        FlushRateMeter.Add(toIndex - fromIndex, measurementTags);
    }

    /// <summary>
    /// Flushes and writes the checkpoint.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous state of the operation.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="NotSupportedException">The flush operation is provided in the background.</exception>
    public async ValueTask FlushAsync(CancellationToken token = default)
    {
        if (flushTrigger is not null)
            throw new NotSupportedException();

        var newSnapshot = SnapshotIndex;
        var newIndex = LastCommittedEntryIndex;

        if (newIndex >= flusherPreviousIndex)
        {
            // Ensure that the flusher is not running with the snapshot installation process concurrently
            lockManager.SetCallerInformation("Flush Pages");
            await lockManager.AcquireReadLockAsync(token).ConfigureAwait(false);
            try
            {
                await Flush(flusherPreviousIndex, newIndex, token).ConfigureAwait(false);
            }
            finally
            {
                lockManager.ReleaseReadLock();
            }
        }

        if ((!cleanupTask.TryGetTarget(out var task) || task.IsCompletedSuccessfully) && flusherOldSnapshotIndex < newSnapshot)
            cleanupTask.SetTarget(CleanUpAsync(newSnapshot, token));

        flusherPreviousIndex = long.Max(flusherOldSnapshotIndex = newSnapshot, newIndex) + 1L;
    }

    /// <inheritdoc cref="IAuditTrail.LastCommittedEntryIndex"/>
    public long LastCommittedEntryIndex
    {
        get => Volatile.Read(in commitIndex);
        private set => Volatile.Write(ref commitIndex, value);
    }
    
    private long Commit(long index)
    {
        var oldCommitIndex = LastCommittedEntryIndex;
        if (index > oldCommitIndex)
        {
            LastCommittedEntryIndex = index;
        }
        else
        {
            index = oldCommitIndex;
        }

        return index - oldCommitIndex;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void OnCommitted(long count)
    {
        applyTrigger.Set();
        flushTrigger?.Set();
        CommitRateMeter.Add(count, measurementTags);
    }
    
    private interface IFlushTrigger
    {
        ValueTask<bool> WaitAsync(CancellationToken token);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct ManualTrigger(AsyncAutoResetEventSlim resetEvent) : IFlushTrigger
    {
        ValueTask<bool> IFlushTrigger.WaitAsync(CancellationToken token)
            => resetEvent.WaitAsync();
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct TimeoutTrigger(TimeSpan timeout) : IFlushTrigger, IDisposable
    {
        private readonly PeriodicTimer timer = new(timeout);
        
        ValueTask<bool> IFlushTrigger.WaitAsync(CancellationToken token)
            => timer.WaitForNextTickAsync(token);

        void IDisposable.Dispose() => timer.Dispose();
    }

    [StructLayout(LayoutKind.Auto)]
    private struct Checkpoint : IDisposable
    {
        private const string FileName = "checkpoint";

        private readonly SafeFileHandle handle;
        private readonly byte[] buffer;

        internal Checkpoint(DirectoryInfo location, out long value)
        {
            var path = Path.Combine(location.FullName, FileName);

            // read the checkpoint
            using (var readHandle = File.OpenHandle(path, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                Span<byte> readBuf = stackalloc byte[sizeof(long)];
                value = RandomAccess.Read(readHandle, readBuf, 0L) >= sizeof(long)
                    ? BinaryPrimitives.ReadInt64LittleEndian(readBuf)
                    : 0L;
            }

            handle = File.OpenHandle(path, FileMode.Open, FileAccess.Write, options: FileOptions.Asynchronous | FileOptions.WriteThrough);
            buffer = GC.AllocateArray<byte>(sizeof(long), pinned: true);
        }

        public ValueTask UpdateAsync(long value, CancellationToken token)
        {
            BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
            return RandomAccess.WriteAsync(handle, buffer, fileOffset: 0L, token);
        }

        public void Dispose()
        {
            handle?.Dispose();
            this = default;
        }
    }
}
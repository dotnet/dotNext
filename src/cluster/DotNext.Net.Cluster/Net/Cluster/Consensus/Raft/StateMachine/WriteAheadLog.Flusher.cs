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
                        Flush(flusherPreviousIndex, newIndex);
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

    private void Flush(long fromIndex, long toIndex)
    {
        var ts = new Timestamp();
        FlushMetadataPages(metadataPages, fromIndex, toIndex);

        var toMetadata = metadataPages[toIndex];
        var fromMetadata = metadataPages[fromIndex];
        FlushDataPages(dataPages, fromMetadata.Offset, toMetadata.End);
        FlushDurationMeter.Record(ts.ElapsedMilliseconds);

        // everything up to toIndex is flushed, save the commit index
        checkpoint.Value = toIndex;

        FlushRateMeter.Add(toIndex - fromIndex, measurementTags);
    }

    private static void FlushMetadataPages(MetadataPageManager metadataPages, long fromIndex, long toIndex)
    {
        var fromPage = metadataPages.GetStartPageIndex(fromIndex);
        var toPage = metadataPages.GetEndPageIndex(toIndex);

        for (var pageIndex = fromPage; pageIndex <= toPage; pageIndex++)
        {
            if (metadataPages.TryGetValue(pageIndex, out var page))
            {
                page.Flush();
            }
        }
    }

    private static void FlushDataPages(PageManager dataPages, ulong fromAddress, ulong toAddress)
    {
        var fromPage = dataPages.GetPageIndex(fromAddress, out _);
        var toPage = dataPages.GetPageIndex(toAddress, out _);

        for (var pageIndex = fromPage; pageIndex <= toPage; pageIndex++)
        {
            if (dataPages.TryGetValue(pageIndex, out var page))
            {
                page.Flush();
            }
        }
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
                Flush(flusherPreviousIndex, newIndex);
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
        private long checkpoint;

        internal Checkpoint(DirectoryInfo location)
        {
            var path = Path.Combine(location.FullName, FileName);
            long preallocationSize;
            FileMode mode;

            if (File.Exists(path))
            {
                preallocationSize = 0L;
                mode = FileMode.Open;
            }
            else
            {
                preallocationSize = sizeof(long);
                mode = FileMode.CreateNew;
            }

            handle = File.OpenHandle(path, mode, FileAccess.ReadWrite, FileShare.Read, FileOptions.WriteThrough, preallocationSize);

            Span<byte> buffer = stackalloc byte[sizeof(long)];
            checkpoint = RandomAccess.Read(handle, buffer, 0L) >= sizeof(long)
                ? BinaryPrimitives.ReadInt64LittleEndian(buffer)
                : 0L;
        }

        internal long Value
        {
            readonly get => checkpoint;

            [SkipLocalsInit]
            set
            {
                Span<byte> buffer = stackalloc byte[sizeof(long)];
                BinaryPrimitives.WriteInt64LittleEndian(buffer, checkpoint = value);
                RandomAccess.Write(handle, buffer, fileOffset: 0L);
            }
        }

        public void Dispose()
        {
            handle?.Dispose();
            this = default;
        }
    }
}
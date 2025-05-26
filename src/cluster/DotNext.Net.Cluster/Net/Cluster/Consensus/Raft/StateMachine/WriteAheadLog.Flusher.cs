using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
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
    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private readonly AsyncAutoResetEvent flushTrigger;

    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private readonly SingleProducerMultipleConsumersCoordinator manualFlushQueue;
    private readonly Task flusherTask;
    private readonly bool flushOnCommit;
    private Checkpoint checkpoint;
    
    private long commitIndex; // Commit lock protects modification of this field

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task FlushAsync(long previousIndex, TimeSpan timeout, CancellationToken token)
    {
        // Weak ref tracks the task, but allows GC to collect associated state machine
        // as soon as possible. While the task is running, it cannot be collected, because it's referenced
        // by the async state machine.
        var cleanupTask = GCHandle.Alloc(Task.CompletedTask, GCHandleType.Weak);
        try
        {
            for (long newIndex, oldSnapshot = SnapshotIndex, newSnapshot;
                 !token.IsCancellationRequested && backgroundTaskFailure is null;
                 previousIndex = long.Max(oldSnapshot = newSnapshot, newIndex) + 1L)
            {
                newSnapshot = SnapshotIndex;
                newIndex = LastCommittedEntryIndex;
                manualFlushQueue.SwitchValve();

                if (newIndex >= previousIndex)
                {
                    // Ensure that the flusher is not running with the snapshot installation process concurrently
                    lockManager.SetCallerInformation("Flush Pages");
                    await lockManager.AcquireReadLockAsync(token).ConfigureAwait(false);
                    try
                    {
                        Flush(previousIndex, newIndex);
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

                if (cleanupTask.Target is null or Task { IsCompletedSuccessfully: true } && oldSnapshot < newSnapshot)
                    cleanupTask.Target = CleanUpAsync(newSnapshot, token);

                manualFlushQueue.Drain();
                await flushTrigger.WaitAsync(timeout, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException e) when (e.CancellationToken == token && cleanupTask.Target is Task target)
        {
            await target.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
        finally
        {
            cleanupTask.Free();
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
    public ValueTask FlushAsync(CancellationToken token = default)
    {
        ValueTask task;

        if (IsDisposingOrDisposed)
        {
            task = new(DisposedTask);
        }
        else
        {
            task = manualFlushQueue.WaitAsync(token);
            flushTrigger.Set();
        }

        return task;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnCommitted(long count)
    {
        if (flushOnCommit)
            flushTrigger.Set();

        applyTrigger.Set();
        CommitRateMeter.Add(count, measurementTags);
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
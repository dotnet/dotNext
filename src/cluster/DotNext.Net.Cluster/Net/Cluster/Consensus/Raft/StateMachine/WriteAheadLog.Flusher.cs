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
    private readonly Task flusherTask;
    private readonly Checkpoint checkpoint;
    
    private long commitIndex; // Commit lock protects modification of this field

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task FlushAsync(long previousIndex, CancellationToken token)
    {
        for (long newIndex; !IsDisposingOrDisposed && backgroundTaskFailure is null; previousIndex = newIndex)
        {
            newIndex = LastCommittedEntryIndex;

            if (newIndex > previousIndex)
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

            if (cleanupTask.IsCompleted)
                cleanupTask = CleanUpAsync(token);
            
            await flushTrigger.WaitAsync(token).ConfigureAwait(false);
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
        var fromPage = GetPageIndex(fromAddress, dataPages.PageSize, out _);
        var toPage = GetPageIndex(toAddress, dataPages.PageSize, out _);

        for (var pageIndex = fromPage; pageIndex <= toPage; pageIndex++)
        {
            if (dataPages.TryGetValue(pageIndex, out var page))
            {
                page.Flush();
            }
        }
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
        flushTrigger.Set();
        applyTrigger.Set();
        CommitRateMeter.Add(count, measurementTags);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct Checkpoint : IDisposable
    {
        private const string FileName = "checkpoint";

        private readonly SafeFileHandle handle;

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
        }

        internal long Value
        {
            [SkipLocalsInit]
            get
            {
                Span<byte> buffer = stackalloc byte[sizeof(long)];
                return RandomAccess.Read(handle, buffer, fileOffset: 0L) == buffer.Length
                    ? BinaryPrimitives.ReadInt64LittleEndian(buffer)
                    : 0L;
            }
            
            [SkipLocalsInit]
            set
            {
                Span<byte> buffer = stackalloc byte[sizeof(long)];
                BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
                RandomAccess.Write(handle, buffer, fileOffset: 0L);
            }
        }

        public void Dispose() => handle?.Dispose();
    }
}
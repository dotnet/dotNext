using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using DotNext.Threading;
using Microsoft.Win32.SafeHandles;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Runtime.CompilerServices;

partial class PersistentState
{
    private readonly ChannelWriter<long> flushTrigger;
    private readonly Task flusherTask;
    private readonly CommitIndexState commitIndexState;

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task FlushAsync(ChannelReader<long> reader)
    {
        var token = reader.Completion.AsCancellationToken();
        for (var previousIndex = commitIndexState.Value; await reader.WaitToReadAsync().ConfigureAwait(false);)
        {
            for (long newIndex; reader.TryRead(out newIndex) && newIndex > previousIndex; previousIndex = newIndex)
            {
                // Ensure that the flusher is not running with the snapshot installation process concurrently
                lockManager.SetCallerInformation("Flush Pages");
                await lockManager.AcquireReadLockAsync(token).ConfigureAwait(false);
                try
                {
                    Flush(previousIndex, newIndex);
                }
                finally
                {
                    lockManager.ReleaseReadLock();
                }
            }
        }
    }

    private void Flush(long fromIndex, long toIndex)
    {
        FlushMetadataPages(metadataPages, fromIndex, toIndex);

        var toMetadata = metadataPages[toIndex];
        var fromMetadata = metadataPages[fromIndex];
        FlushDataPages(dataPages, fromMetadata.Offset, toMetadata.End);

        // everything up to toIndex is flushed, save the commit index
        commitIndexState.Value = toIndex;
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
                page.DisposeAndDelete();
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
                page.DisposeAndDelete();
            }
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct CommitIndexState : IDisposable
    {
        private const string FileName = "commitIndex";

        private readonly SafeFileHandle handle;

        internal CommitIndexState(DirectoryInfo location)
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
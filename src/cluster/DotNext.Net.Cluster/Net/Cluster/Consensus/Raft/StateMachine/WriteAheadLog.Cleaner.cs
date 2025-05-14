using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DotNext.Threading;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Runtime.CompilerServices;

partial class WriteAheadLog
{
    private readonly Task cleanupTask;

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task CleanUpAsync(long previousIndex, CancellationToken token)
    {
        for (long nextIndex; !IsDisposingOrDisposed; previousIndex = nextIndex)
        {
            nextIndex = stateMachine.TakeSnapshot()?.Index ?? 0L;
            
            // After the barrier, we know that there is no competing reader that reads the old snapshot version
            lockManager.SetCallerInformation("Remove Pages");
            await lockManager.AcquireReadBarrierAsync(token).ConfigureAwait(false);
            try
            {
                // The barrier can suspend this async flow. However, the OS flushes the pages in the background
                RemoveSquashedPages(previousIndex, nextIndex);

                // ensure that garbage reclamation is not running concurrently with the snapshot installation process
                await stateMachine.ReclaimGarbageAsync(nextIndex, token).ConfigureAwait(false);
            }
            finally
            {
                lockManager.ReleaseReadLock();
            }
            
            await appliedEvent.WaitAsync(token).ConfigureAwait(false);
        }
    }

    private void RemoveSquashedPages(long fromIndex, long toIndex)
    {
        LogEntryMetadata metadata;
        while (!metadataPages.TryGetMetadata(fromIndex, out metadata))
        {
            fromIndex++;
        }

        if (fromIndex >= toIndex || !metadataPages.TryGetMetadata(toIndex, out metadata))
            return;
        
        var fromPage = GetPageIndex(metadata.Offset, dataPages.PageSize, out _);
        var toPage = GetPageIndex(metadata.End, dataPages.PageSize, out _);
        var removedDataBytes = dataPages.Delete(fromPage, toPage) * (long)dataPages.PageSize;

        fromPage = metadataPages.GetStartPageIndex(fromIndex);
        toPage = metadataPages.GetEndPageIndex(toIndex);
        var removedMetadataBytes = metadataPages.Delete(fromPage, toPage) * (long)metadataPages.PageSize;
        
        BytesDeletedMeter.Record(removedDataBytes + removedMetadataBytes, measurementTags);
    }
}
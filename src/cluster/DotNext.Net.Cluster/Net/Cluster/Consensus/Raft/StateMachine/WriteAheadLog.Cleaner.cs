using System.Runtime.CompilerServices;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Runtime.CompilerServices;
using Threading;

partial class WriteAheadLog
{
    // Tracks the highest index actually populated via WriteMetadata (never touched by snapshot
    // installation, which can jump LastEntryIndex over a range of indices whose metadata slots
    // were never written). Used by RemoveSquashedPages to avoid trusting a snapshot's own,
    // possibly-garbage metadata slot when computing the data-page deletion boundary.
    private long lastWrittenIndex;
    
    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task CleanUpAsync(long upToIndex, CancellationToken token)
    {
        // After the barrier, we know that there is no competing reader that reads the old snapshot version
        lockManager.SetCallerInformation("Remove Pages");
        await lockManager.AcquireReadBarrierAsync(token).ConfigureAwait(false);
        try
        {
            // The barrier can suspend this async flow. However, the OS flushes the pages in the background
            RemoveSquashedPages(upToIndex);

            // ensure that garbage reclamation is not running concurrently with the snapshot installation process
            await stateMachine.ReclaimGarbageAsync(upToIndex, token).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException canceledEx || canceledEx.CancellationToken != token)
        {
            backgroundTaskFailure = e;
            appliedEvent.Interrupt(new InternalException(e));
        }
        finally
        {
            lockManager.ReleaseReadLock();
        }
    }

    private void RemoveSquashedPages(long toIndex)
    {
        var lastWrittenIndexCopy = Atomic.Read(in lastWrittenIndex);
        
        long removedBytes;
        LogEntryMetadata metadata;
        switch (lastWrittenIndexCopy.CompareTo(toIndex))
        {
            case <= 0 when lastWrittenIndexCopy is not 0L
                           && metadataPages.TryGetMetadata(lastWrittenIndexCopy, out metadata):
                removedBytes = dataPages.DeletePages(metadata.End);
                Interlocked.CompareExchange(ref lastWrittenIndex, 0L, lastWrittenIndexCopy);
                break;
            case > 0 when metadataPages.TryGetMetadata(toIndex + 1L, out metadata):
                removedBytes = dataPages.DeletePages(metadata.Offset);
                break;
            default:
                removedBytes = 0L;
                break;
        }

        removedBytes += metadataPages.DeletePages(toIndex);
        if (removedBytes > 0L)
            BytesDeletedMeter.Record(removedBytes, measurementTags);
    }
}
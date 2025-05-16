using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Runtime.CompilerServices;

partial class WriteAheadLog
{
    private Task cleanupTask = Task.CompletedTask;

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task CleanUpAsync(CancellationToken token)
    {
        var nextIndex = stateMachine.TakeSnapshot()?.Index ?? 0L;

        // After the barrier, we know that there is no competing reader that reads the old snapshot version
        lockManager.SetCallerInformation("Remove Pages");
        await lockManager.AcquireReadBarrierAsync(token).ConfigureAwait(false);
        try
        {
            // The barrier can suspend this async flow. However, the OS flushes the pages in the background
            RemoveSquashedPages(nextIndex);

            // ensure that garbage reclamation is not running concurrently with the snapshot installation process
            await stateMachine.ReclaimGarbageAsync(nextIndex, token).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException canceledEx || canceledEx.CancellationToken != token)
        {
            backgroundTaskFailure = ExceptionDispatchInfo.Capture(e);
            appliedEvent.Interrupt(e);
        }
        finally
        {
            lockManager.ReleaseReadLock();
        }
    }

    private void RemoveSquashedPages(long toIndex)
    {
        if (!metadataPages.TryGetMetadata(toIndex, out var metadata))
            return;

        var toPage = dataPages.GetPageIndex(metadata.End, out _);
        var removedBytes = dataPages.Delete(toPage) * (long)dataPages.PageSize;

        toPage = metadataPages.GetEndPageIndex(toIndex);
        removedBytes += metadataPages.Delete(toPage) * (long)MetadataPageManager.PageSize;

        BytesDeletedMeter.Record(removedBytes, measurementTags);
    }
}
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Runtime.CompilerServices;

partial class WriteAheadLog
{
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

        var removedBytes = dataPages.DeletePages(metadata.End) + metadataPages.DeletePages(toIndex);
        if (removedBytes > 0L)
            BytesDeletedMeter.Record(removedBytes, measurementTags);
    }
}
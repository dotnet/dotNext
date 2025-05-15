using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using DotNext.Threading;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Runtime.CompilerServices;

partial class WriteAheadLog
{
    private readonly Task cleanupTask;

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task CleanUpAsync(CancellationToken token)
    {
        while (!IsDisposingOrDisposed)
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
                break;
            }
            finally
            {
                lockManager.ReleaseReadLock();
            }
            
            await appliedEvent.WaitAsync(token).ConfigureAwait(false);
        }
    }

    private void RemoveSquashedPages(long toIndex)
    {
        if (!metadataPages.TryGetMetadata(toIndex, out var metadata))
            return;

        var toPage = dataPages.GetPageIndex(metadata.End, out _);
        var removedBytes = dataPages.Delete(toPage) * (long)dataPages.PageSize;

        toPage = metadataPages.GetEndPageIndex(toIndex);
        removedBytes += dataPages.Delete(toPage) * (long)MetadataPageManager.PageSize;

        BytesDeletedMeter.Record(removedBytes, measurementTags);
    }
}
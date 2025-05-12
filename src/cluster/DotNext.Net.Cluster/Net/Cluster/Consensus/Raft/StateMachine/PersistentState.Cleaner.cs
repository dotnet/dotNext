using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DotNext.Threading;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Runtime.CompilerServices;

partial class PersistentState
{
    private readonly ChannelWriter<long> cleanupTrigger;
    private readonly Task cleanupTask;

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task CleanUpAsync(ChannelReader<long> reader)
    {
        var token = reader.Completion.AsCancellationToken();
        for (var previousIndex = 0L; await reader.WaitToReadAsync().ConfigureAwait(false);)
        {
            for (long nextIndex; reader.TryRead(out nextIndex); previousIndex = nextIndex)
            {
                // After the barrier, we know that there is no competing reader that reads the old snapshot version
                lockManager.SetCallerInformation("Remove Pages");
                await lockManager.AcquireReadBarrierAsync(token).ConfigureAwait(false);
                lockManager.ReleaseReadBarrier();

                // The barrier can suspend this async flow. However, the OS flushes the pages in the background
                RemoveSquashedPages(previousIndex, nextIndex);

                // ensure that garbage reclamation is not running concurrently with the snapshot installation process
                await lockManager.AcquireReadLockAsync(token).ConfigureAwait(false);
                try
                {
                    await ReclaimGarbageAsync(nextIndex, token).ConfigureAwait(false);
                }
                finally
                {
                    lockManager.ReleaseReadLock();
                }
            }
        }
    }

    private void RemoveSquashedPages(long fromIndex, long toIndex)
    {
        var address = metadataPages[fromIndex].Offset;
        var fromPage = GetPageIndex(address, dataPages.PageSize, out _);
        address = metadataPages[toIndex].End;
        var toPage = GetPageIndex(address, dataPages.PageSize, out _);
        dataPages.Delete(fromPage, toPage);

        fromPage = metadataPages.GetStartPageIndex(fromIndex);
        toPage = metadataPages.GetEndPageIndex(toIndex);
        metadataPages.Delete(fromPage, toPage);
    }
}